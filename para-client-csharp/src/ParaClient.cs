/*
 * Copyright 2013-2019. https://erudika.com
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * For issues and patches go to: https://github.com/erudika
 */
using System;
using System.Collections.Generic;
using Amazon.Runtime.Internal.Auth;
using Amazon.Runtime.Internal;
using Amazon.Runtime;
using RestSharp;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Para.Client
{
    /// <summary>
    /// The .NET REST client for communicating with a Para API server.
    /// </summary>
    public class ParaClient
    {
        static readonly DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        static readonly string DEFAULT_ENDPOINT = "https://paraio.com";
        static readonly string DEFAULT_PATH = "/v1/";
        static readonly string JWT_PATH = "/jwt_auth";
        static readonly string SEPARATOR = ":";
        string endpoint;
        string path;
        string tokenKey;
        long tokenKeyExpires = -1;
        long tokenKeyNextRefresh = -1;
        string accessKey;
        string secretKey;

        readonly AWS4Signer signer = new AWS4Signer();
        readonly RestClient client = new RestClient();
        readonly EventLog logger = new EventLog();

        public ParaClient(string accessKey, string secretKey)
        {            
            this.accessKey = accessKey;
            this.secretKey = secretKey;
            setEndpoint(DEFAULT_ENDPOINT);
            setApiPath(DEFAULT_PATH);
            logger.Source = "Application";
            if (secretKey == null || secretKey.Length < 6) {
                logger.WriteEntry("Secret key appears to be invalid. Make sure you call 'signIn()' first.");
            }
        }

        public void setEndpoint(string endpoint)
        {
            this.endpoint = endpoint;            
        }
        
        /// <summary>
        /// Returns the App for the current access key (appid).
        /// </summary>
        /// <returns>the App object</returns>
        public ParaObject getApp()
        {
            return me();
        }
        
        /// <summary>
        /// Returns the endpoint URL
        /// </summary>
        /// <returns>the endpoint</returns>
        public string getEndpoint()
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                return DEFAULT_ENDPOINT;
            }
            else
            {
                return endpoint;
            }
        }

        /// <summary>
        /// Sets the API request path
        /// </summary>
        /// <param name="path">a new path</param>
        public void setApiPath(string path)
        {
            this.path = path;
        }

        /// <summary>
        /// Returns the API request path
        /// </summary>
        /// <returns>the request path without parameters</returns>
        public string getApiPath()
        {
            if (string.IsNullOrEmpty(path))
            {
                return DEFAULT_PATH;
            }
            else
            {
                if (!path.EndsWith("/"))
                {
                    path += "/";
                }
                return path;
            }
        }

        /// <summary>
        /// Gets the Para server version.
        /// </summary>
        /// <returns>the version of Para server</returns>
        public string getServerVersion()
        {
            Dictionary<string, object> res = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                (string)getEntity(invokeGet("", null), true));
            if (res == null || string.IsNullOrEmpty((String) res["version"])) {
                return "unknown";
            } else {
                return (String) res["version"];
            }
        }

        /// <returns>the JWT access token, or null if not signed in</returns>
        public string getAccessToken()
        {
            return tokenKey;
        }

        /// <summary>
        /// Sets the JWT access token.
        /// </summary>
        /// <param name="token">a valid token.</param>
        public void setAccessToken(string token)
        {
            if (!string.IsNullOrEmpty(token)) {
                try {
                    var parts = token.Split('.');
                    var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                    var decoded = JsonConvert.DeserializeObject<Dictionary<string, object>>(payload);
                    if (decoded != null && decoded.ContainsKey("exp")) {
                        tokenKeyExpires = (long)decoded ["exp"];
                        tokenKeyNextRefresh = (long)decoded ["refresh"];
                    }
                } catch {
                    tokenKeyExpires = -1;
                    tokenKeyNextRefresh = -1;
                }
            }
            tokenKey = token;
        }

        /// <summary>
        /// Clears the JWT token from memory, if such exists.
        /// </summary>
        void clearAccessToken()
        {
            tokenKey = null;
            tokenKeyExpires = -1;
            tokenKeyNextRefresh = -1;
        }

        /// <summary>
        /// Deserializes a IRestResponse object to POJO of some type.
        /// </summary>
        /// <returns>A ParaObject.</returns>
        /// <param name="res">response</param>
        /// <param name="returnRawJSON">If set to <c>true</c> returns the raw JSON string.</param>
        public object getEntity(IRestResponse res, bool returnRawJSON)
        {
            if (res != null)
            {
                if (res.StatusCode == HttpStatusCode.OK
                        || res.StatusCode == HttpStatusCode.Created
                        || res.StatusCode == HttpStatusCode.NotModified)
                {
                    if (returnRawJSON)
                    {
						return res.Content;
                    }
                    else
                    {
                        return new ParaObject().setFields((Dictionary<string, object>) Deserialize(res.Content));
                    }
                }
                else if (res.StatusCode != HttpStatusCode.NotFound
                      && res.StatusCode != HttpStatusCode.NotModified
                      && res.StatusCode != HttpStatusCode.NoContent)
                {
                    var error = (res.Content != null) ?
                            JsonConvert.DeserializeObject<Dictionary<string, object>>(res.Content) : null;
                    if (error != null && error.ContainsKey("code"))
                    {
                        string msg = error.ContainsKey("message") ? (string)error["message"] : "error";
                        logger.WriteEntry(msg + " - " + error["code"]);
                    }
                    else
                    {
                        logger.WriteEntry(res.StatusCode + " - " + res.StatusDescription);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the full resource path, e.g. "/v1/path".
        /// </summary>
        /// <returns>The full resource path, e.g. "/v1/path".</returns>
        /// <param name="resourcePath">API subpath.</param>
        protected string getFullPath(string resourcePath)
        {
            if (resourcePath != null && resourcePath.StartsWith(JWT_PATH)) {
                return resourcePath;
            }
            if (string.IsNullOrEmpty(resourcePath))
            {
                resourcePath = "";
            }
            else if (resourcePath.StartsWith("/"))
            {
                resourcePath = resourcePath.Substring(1);
            }
            return getApiPath() + resourcePath;
        }

        long CurrentTimeMillis()
        {
            return (long) (DateTime.UtcNow - Jan1st1970).TotalMilliseconds;
        }

        static object Deserialize(string json)
        {
            return (json != null) ? ToObject(JToken.Parse(json)) : null;
        }

        protected static object ToObject(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    return token.Children<JProperty>().ToDictionary(prop => prop.Name, prop => ToObject(prop.Value));

                case JTokenType.Array:
                    return token.Select(ToObject).ToList();

                default:
                    return ((JValue)token).Value;
            }
        }

        protected IRestResponse invokeSignedRequest(Method httpMethod, string endpointURL, string reqPath,
			Dictionary<string, string> headers, Dictionary<string, object> paramz, object jsonEntity)
        {
            if (string.IsNullOrEmpty(accessKey))
            {
                throw new Exception("Blank access key: " + httpMethod + " " + reqPath);
            }
            bool doSign = true;
            if (string.IsNullOrEmpty(secretKey) && string.IsNullOrEmpty(tokenKey)) {
                if (headers == null) {
                    headers = new Dictionary<string, string>();
                }
                headers["Authorization"] = "Anonymous " + accessKey;
                doSign = false;
            }

            var req = new DefaultRequest(new ParaRequest(), "para");
            req.Endpoint = new Uri(endpointURL);
            req.ResourcePath = reqPath;
            req.HttpMethod = httpMethod.ToString();
            req.UseQueryString = true;

            var restReq = new RestRequest(new Uri(endpointURL));
            restReq.Method = httpMethod;
            client.BaseUrl = new Uri(endpoint + reqPath);

            if (paramz != null)
            {
                foreach (var param in paramz)
                {
                    if (param.Value == null) continue;
                    if (param.Value.GetType() == typeof(List<string>))
                    {
                        if (((List<string>) param.Value).Count > 0) {
                            req.Parameters.Add(param.Key, ((List<string>) param.Value).First());
                            foreach (var val in (List<string>) param.Value)
                            {
                                if (val != null)
                                {
                                    restReq.AddQueryParameter(param.Key, val);
                                }
                            }
                        }
                    }
                    else
                    {
                        req.Parameters.Add(param.Key, (string) param.Value);
                        restReq.AddQueryParameter(param.Key, (string) param.Value);
                    }
                }
            }

            if (headers != null)
            {
                foreach (var key in headers.Keys) {
                    req.Headers[key] = headers[key];
                }
            }
            if (jsonEntity != null)
            {
                string json = JsonConvert.SerializeObject(jsonEntity, Formatting.None);
                restReq.AddParameter("application/json", json, ParameterType.RequestBody);
                restReq.AddHeader("Content-Type", "application/json");
                req.Headers.Add("Content-Type", "application/json");
                req.Content = System.Text.Encoding.UTF8.GetBytes(json);
            }

            if (tokenKey != null)
            {
                // make sure you don't create an infinite loop!
                if (!(httpMethod.ToString() == "GET" && reqPath == JWT_PATH))
                {
                    refreshToken();
                }
                req.Headers.Add("Authorization", "Bearer " + tokenKey);
            } 
            else if (doSign)
            {
                try
                {
                    var p = new ParaConfig();
                    p.ServiceURL = getEndpoint();
                    p.AuthenticationServiceName = "para";
                    signer.Sign(req, p, null, accessKey, secretKey);
                }
                catch
                {                
                    return null;
                }
            }

            if (req.Headers != null)
            {
                foreach (var header in req.Headers)
                {
                    restReq.AddHeader(header.Key, header.Value);
                }
            }
            return client.Execute(restReq);
        }

        /// <summary>
        /// Invoke a GET request to the Para API.
        /// </summary>
        /// <returns>response object</returns>
        /// <param name="resourcePath">the subpath after '/v1/', should not start with '/'</param>
        /// <param name="paramz">query parameters</param>
		public IRestResponse invokeGet(string resourcePath, Dictionary<string, object> paramz)
        {
            return invokeSignedRequest(Method.GET, getEndpoint(), getFullPath(resourcePath), null, paramz, null);
        }

        /// <summary>
        /// Invoke a POST request to the Para API.
        /// </summary>
        /// <returns>response object</returns>
        /// <param name="resourcePath">the subpath after '/v1/', should not start with '/'</param>
        /// <param name="entity">request body</param>
        public IRestResponse invokePost(string resourcePath, object entity)
        {
            return invokeSignedRequest(Method.POST, getEndpoint(), getFullPath(resourcePath), null, null, entity);
        }

        /// <summary>
        /// Invoke a PUT request to the Para API.
        /// </summary>
        /// <returns>response object</returns>
        /// <param name="resourcePath">the subpath after '/v1/', should not start with '/'</param>
        /// <param name="entity">request body</param>
        public IRestResponse invokePut(string resourcePath, object entity)
        {
            return invokeSignedRequest(Method.PUT, getEndpoint(), getFullPath(resourcePath), null, null, entity);
        }

        /// <summary>
        /// Invoke a PATCH request to the Para API.
        /// </summary>
        /// <returns>response object</returns>
        /// <param name="resourcePath">the subpath after '/v1/', should not start with '/'</param>
        /// <param name="entity">request body</param>
        public IRestResponse invokePatch(string resourcePath, object entity)
        {
            return invokeSignedRequest(Method.PATCH, getEndpoint(), getFullPath(resourcePath), null, null, entity);
        }

        /// <summary>
        /// Invoke a PATCH request to the Para API.
        /// </summary>
        /// <returns>response object</returns>
        /// <param name="resourcePath">the subpath after '/v1/', should not start with '/'</param>
        /// <param name="paramz">query parameters</param>
		public IRestResponse invokeDelete(string resourcePath, Dictionary<string, object> paramz)
        {
            return invokeSignedRequest(Method.DELETE, getEndpoint(), getFullPath(resourcePath), null, paramz, new byte[0]);
        }

        /// <summary>
        /// Converts a Pager object to query parameters.
        /// </summary>
        /// <returns>list of query parameters</returns>
        /// <param name="pager">a Pager</param>
        public Dictionary<string, object> pagerToParams(params Pager[] pager)
        {
            var map = new Dictionary<string, object>();
            if (pager != null && pager.Length > 0)
            {
                Pager p = pager[0];
                if (p != null)
                {
                    map["page"] = p.page.ToString();
                    map["desc"] = p.desc.ToString();
                    map["limit"] = p.limit.ToString();
                    if (p.lastKey != null)
                    {
                        map["lastKey"] = p.lastKey;
                    }
                    if (p.sortby != null)
                    {
                        map["sort"] = p.sortby;
                    }
                }
            }
            return map;
        }

        /// <summary>
        /// Deserializes ParaObjects from a JSON array (the "items:[]" field in search results).
        /// </summary>
        /// <returns>a list of deserialized maps</returns>
        /// <param name="res">a list of ParaObjects</param>
        public List<ParaObject> getItemsFromList(object res)
        {
            if (res != null)
            {
                List<object> result = null;
                if (res.GetType() == typeof(List<object>))
                {
                    result = (List<object>) res;
                }
                else if (res is string)
                {
					result = (List<object>) Deserialize((string)res);
				}

                if (result != null && result.Count > 0)
                {
                    // this isn't very efficient but there's no way to know what type of objects we're reading
                    var objects = new List<ParaObject>(result.Count);
                    foreach (object properties in result)
                    {
                        if (properties != null)
                        {
                            objects.Add(new ParaObject().setFields((Dictionary<string, object>) properties));
                        }
                    }
                    return objects;
                }
            }
            return new List<ParaObject>();
        }

        /// <summary>
        /// Converts a list of Maps to a List of ParaObjects, at a given path within the JSON tree structure.
        /// </summary>
        /// <returns>a list of ParaObjects</returns>
        /// <param name="res">the response body for an API request</param>
        /// <param name="pager">a Pager</param>
        public List<ParaObject> getItems(object res, string at, params Pager[] pager)
        {
            if (res != null)
            {
                Dictionary<string, object> result = null;
                if (res.GetType() == typeof(Dictionary<string, object>))
                {
                    result = (Dictionary<string, object>) res;
                }
				else if (res is string) 
                {
					result = (Dictionary<string, object>) Deserialize((string)res);
				}

                if (result != null && result.Count > 0 && at != null && result.ContainsKey(at))
                {
                    if (pager != null && pager.Length > 0 && pager[0] != null)
                    {
                        if (result.ContainsKey("totalHits"))
                        {
							pager[0].count = (long) result["totalHits"];
                        }
                        if (result.ContainsKey("lastKey"))
                        {
                            pager[0].lastKey = (string) result["lastKey"];
                        }
                    }
                    return getItemsFromList(result[at]);
                }
            }
            return new List<ParaObject>();
        }

        private List<ParaObject> getItems(object res, params Pager[] pager)
        {
            return getItems(res, "items", pager);
        }

        /////////////////////////////////////////////
        //				 PERSISTENCE
        /////////////////////////////////////////////

        /// <summary>
        /// Persists an object to the data store. If the object's type and id are given,
        /// then the request will be a PUT request and any existing object will be overwritten.
        /// </summary>
        /// <param name="obj">the domain object</param>
        /// <returns>the same object with assigned id or null if not created.</returns>
        public ParaObject create(ParaObject obj)
        {
            if (obj == null)
            {
                return null;
            }
            if (string.IsNullOrEmpty(obj.id) || string.IsNullOrEmpty(obj.type))
            {
                return (ParaObject) getEntity(invokePost(obj.type, obj), false);
            }
            else
            {
                return (ParaObject) getEntity(invokePut(obj.type + "/" + obj.id, obj), false);
            }
        }

        /// <summary>
        /// Retrieves an object from the data store.
        /// </summary>
        /// <param name="type">the type of the object</param>
        /// <param name="id">the id of the object</param>
        /// <returns>the retrieved object or null if not found</returns>
        public ParaObject read(string type, string id)
        {
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(id))
            {
                return null;
            }
            return (ParaObject) getEntity(invokeGet(type + "/" + id, null), false);
        }

        /// <summary>
        /// Retrieves an object from the data store.
        /// </summary>
        /// <param name="id">the id of the object</param>
        /// <returns>the retrieved object or null if not found</returns>
        public ParaObject read(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            return (ParaObject) getEntity(invokeGet("_id/" + id, null), false);
        }
        
        /// <summary>
        /// Updates an object permanently. Supports partial updates.
        /// </summary>
        /// <param name="obj">the object to update</param>
        /// <returns>the updated object</returns>
        public ParaObject update(ParaObject obj)
        {
            if (obj == null)
            {
                return null;
            }
            return (ParaObject) getEntity(invokePatch(obj.getObjectURI(), obj), false);
        }
        
        /// <summary>
        /// Deletes an object permanently.
        /// </summary>
        /// <param name="obj">the object to delete</param>
        public void delete(ParaObject obj)
        {
            if (obj == null)
            {
                return;
            }
            invokeDelete(obj.getObjectURI(), null);
        }
        
        /// <summary>
        /// Saves multiple objects to the data store.
        /// </summary>
        /// <param name="objects">the list of objects to save</param>
        /// <returns>a list of objects</returns>
        public List<ParaObject> createAll(List<ParaObject> objects)
        {
            if (objects == null || objects.Count == 0 || objects[0] == null)
            {
                return new List<ParaObject>(0);
            }            
            return getItemsFromList(getEntity(invokePost("_batch", objects), true));
        }
        
        /// <summary>
        /// Retrieves multiple objects from the data store.
        /// </summary>
        /// <param name="keys">a list of object ids</param>
        /// <returns>a list of objects</returns>
        public List<ParaObject> readAll(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return new List<ParaObject>(0);
            }
            var ids = new Dictionary<string, object>();
            ids["ids"] = keys;
            return getItemsFromList(getEntity(invokeGet("_batch", ids), true));
    	}
        
        /// <summary>
        /// Updates multiple objects.
        /// </summary>
        /// <param name="objects">the objects to update</param>
        /// <returns>a list of objects</returns>
        public List<ParaObject> updateAll(List<ParaObject> objects)
        {
            if (objects == null || objects.Count == 0)
            {
                return new List<ParaObject>(0);
            }
            return getItemsFromList(getEntity(invokePatch("_batch", objects), true));
    	}
        
        /// <summary>
        /// Deletes multiple objects.
        /// </summary>
        /// <param name="keys">the ids of the objects to delete</param>
        public void deleteAll(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return;
            }
            var ids = new Dictionary<string, object>();
            ids["ids"] = keys;
            invokeDelete("_batch", ids);
        }

        /// <summary>
        /// Returns a list all objects found for the given type.
        /// The result is paginated so only one page of items is returned, at a time.
        /// </summary>
        /// <param name="type">the type of objects to search for</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of objects</returns>
        public List<ParaObject> list(string type, params Pager[] pager)
        {
            if (string.IsNullOrEmpty(type))
            {
                return new List<ParaObject>(0);
            }
            return getItems(getEntity(invokeGet(type, pagerToParams(pager)), true), pager);
    	}

        /////////////////////////////////////////////
        //				 SEARCH
        /////////////////////////////////////////////
        
        /// <summary>
        /// Simple id search.
        /// </summary>
        /// <param name="id">the id</param>
        /// <returns>the object if found or null</returns>
        public ParaObject findById(string id)
        {
            var paramz = new Dictionary<string, object>();
		    paramz["id"] = id;
            var items = getItems(find("id", paramz));
            return items.Count == 0 ? null : items[0];
        }

        /// <summary>
        /// Simple multi id search.
        /// </summary>
        /// <param name="ids">a list of ids to search for</param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findByIds(List<string> ids)
        {
            var paramz = new Dictionary<string, object>();
        	paramz["ids"] = ids;
            return getItems(find("ids", paramz));
        }

        /// <summary>
        /// Search for {@link com.erudika.para.core.Address} objects in a radius of X km from a given point.
        /// </summary>
        /// <param name="type">the type of object to search for.</param>
        /// <param name="query">the query string</param>
        /// <param name="radius">the radius of the search circle</param>
        /// <param name="lat">latitude</param>
        /// <param name="lng">longitude</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findNearby(string type, string query, int radius, double lat, double lng, params Pager[] pager)
        {
            var paramz = new Dictionary<string, object>();
        	paramz["latlng"] = lat.ToString(CultureInfo.CreateSpecificCulture("en-GB")) + "," + 
                lng.ToString(CultureInfo.CreateSpecificCulture("en-GB"));
        	paramz["radius"] = radius.ToString();
        	paramz["q"] = query;
        	paramz["type"] = type;
        	paramz.Concat(pagerToParams(pager));
            return getItems(find("nearby", paramz), pager);
        }

        /// <summary>
        /// Searches for objects that have a property which value starts with a given prefix.
        /// </summary>
        /// <param name="type">the type of object to search for.</param>
        /// <param name="field">the property name of an object</param>
        /// <param name="prefix">the prefix</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findPrefix(string type, string field, string prefix, params Pager[] pager)
        {
            var paramz = new Dictionary<string, object>();
        	paramz["field"] = field;
        	paramz["prefix"] = prefix;
        	paramz["type"] = type;
        	paramz.Concat(pagerToParams(pager));
            return getItems(find("prefix", paramz), pager);
        }

        /// <summary>
        /// Simple query string search. This is the basic search method.
        /// </summary>
        /// <param name="type">the type of object to search for.</param>
        /// <param name="query">the query string</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findQuery(string type, string query, params Pager[] pager)
        {
            var paramz = new Dictionary<string, object>();
            paramz["q"] = query;
            paramz["type"] = type;
            paramz.Concat(pagerToParams(pager));
            return getItems(find("", paramz), pager);
        }

        /// <summary>
        /// Searches within a nested field. The objects of the given type must contain a nested field "nstd".
        /// </summary>
        /// <param name="type">the type of object to search for.</param>
        /// <param name="field">the name of the field to target (within a nested field "nstd")</param>
        /// <param name="query">the query string</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findNestedQuery(string type, string field, string query, params Pager[] pager)
        {
            var paramz = new Dictionary<string, object>();
            paramz["q"] = query;
            paramz["field"] = field;
            paramz["type"] = type;
            paramz.Concat(pagerToParams(pager));
            return getItems(find("nested", paramz), pager);
        }

        /// <summary>
        /// Searches for objects that have similar property values to a given text. A "find like this" query.
        /// </summary>
        /// <param name="type">the type of object to search for.</param>
        /// <param name="filterKey">exclude an object with this key from the results (optional)</param>
        /// <param name="fields">a list of property names</param>
        /// <param name="liketext">text to compare to</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findSimilar(string type, string filterKey, string[] fields, string liketext, params Pager[] pager)
        {
            var paramz = new Dictionary<string, object>();
        	paramz["fields"] = (fields == null) ? null : fields.ToList();
            paramz["filterid"] = filterKey;
            paramz["like"] = liketext;
            paramz["type"] = type;
            paramz.Concat(pagerToParams(pager));
            return getItems(find("similar", paramz), pager);
        }

        /// <summary>
        /// Searches for objects tagged with one or more tags.
        /// </summary>
        /// <param name="type">the type of object to search for.</param>
        /// <param name="tags">the list of tags</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findTagged(string type, string[] tags, params Pager[] pager)
        {
            var paramz = new Dictionary<string, object>();
        	paramz["tags"] = (tags == null) ? null : tags.ToList();
            paramz["type"] = type;
            paramz.Concat(pagerToParams(pager));
            return getItems(find("tagged", paramz), pager);
        }

        /// <summary>
        /// Searches for Tag objects.
        /// This method might be deprecated in the future.
        /// </summary>
        /// <param name="keyword">the tag keyword to search for</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findTags(string keyword, params Pager[] pager)
        {
            keyword = (keyword == null) ? "*" : keyword + "*";
            return findWildcard("tag", "tag", keyword, pager);
        }

        /// <summary>
        /// Searches for objects having a property value that is in list of possible values.
        /// </summary>
        /// <param name="type">the type of object to search for.</param>
        /// <param name="field">the property name of an object</param>
        /// <param name="terms">a list of terms (property values)</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findTermInList(string type, string field, List<string> terms, params Pager[] pager)
        {
            var paramz = new Dictionary<string, object>();
            paramz["field"] = field;
    		paramz["terms"] = terms;
            paramz["type"] = type;
            paramz.Concat(pagerToParams(pager));
            return getItems(find("in", paramz), pager);
        }
        
        /// <summary>
        /// Searches for objects that have properties matching some given values. A terms query.
        /// </summary>
        /// <param name="type">the type of object to search for</param>
        /// <param name="terms">a Dictionary of fields (property names) to terms (property values)</param>
        /// <param name="matchAll">match all terms. If true - AND search, if false - OR search</param>
        /// <param name="pager"></param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findTerms(string type, Dictionary<string, object> terms, bool matchAll, params Pager[] pager)
        {
            if (terms == null)
            {
                return new List<ParaObject>(0);
            }
            var paramz = new Dictionary<string, object>();
        	paramz["matchall"] = matchAll.ToString();
            var termz = new List<string>();
            foreach (var term in terms)
            {
                string key = term.Key;
                object value = term.Value;
                if (value != null)
                {
                    termz.Add(key + SEPARATOR + value);
                }
            }
            if (terms.Count > 0)
            {
        		paramz["terms"] = termz;
            }
        	paramz["type"] = type;
        	paramz.Concat(pagerToParams(pager));
            return getItems(find("terms", paramz), pager);
        }
        
        /// <summary>
        /// Searches for objects that have a property with a value matching a wildcard query.
        /// </summary>
        /// <param name="type">the type of object to search for.</param>
        /// <param name="field">the property name of an object</param>
        /// <param name="wildcard">wildcard query string. For example "cat*".</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of objects found</returns>
        public List<ParaObject> findWildcard(string type, string field, string wildcard, params Pager[] pager)
        {
            var paramz = new Dictionary<string, object>();
            paramz["field"] = field;
            paramz["q"] = wildcard;
            paramz["type"] = type;
            paramz.Concat(pagerToParams(pager));
            return getItems(find("wildcard", paramz), pager);
        }
        
        /// <summary>
        /// Counts indexed objects.
        /// </summary>
        /// <param name="type">the type of object to search for.</param>
        /// <returns>the number of results found</returns>
        public long getCount(string type)
        {
            var paramz = new Dictionary<string, object>();
            paramz["type"] = type;
            var pager = new Pager();
            getItems(find("count", paramz), pager);
            return pager.count;
        }
        
        /// <summary>
        /// Counts indexed objects matching a set of terms/values.
        /// </summary>
        /// <param name="type">the type of object to search for.</param>
        /// <param name="terms">a list of terms (property values)</param>
        /// <returns>the number of results found</returns>
        public long getCount(string type, Dictionary<string, object> terms)
        {
            if (terms == null)
            {
                return 0L;
            }
            var paramz = new Dictionary<string, object>();
            var termz = new List<string>();
            foreach (var term in terms)
            {
                string key = term.Key;
                object value = term.Value;
                if (value != null)
                {
                    termz.Add(key + SEPARATOR + value);
                }
            }
            if (terms.Count > 0)
            {
        		paramz["terms"] = termz;
            }
            paramz["type"] = type;
            paramz["count"] = "true";
            var pager = new Pager();
            getItems(find("terms", paramz), pager);
            return pager.count;
        }

		object find(string queryType, Dictionary<string, object> paramz)
        {
            var map = new Dictionary<string, object>();
            if (paramz != null && paramz.Count > 0) {
                string qType = string.IsNullOrEmpty(queryType) ? "/default" : "/" + queryType;
                if (!paramz.ContainsKey("type") || string.IsNullOrEmpty((string) paramz["type"]))
                {
                    return getEntity(invokeGet("search" + qType, paramz), true);
                }
                else
                {
                    return getEntity(invokeGet(paramz["type"] + "/search" + qType, paramz), true);
                }
        	}
            else
            {
        		map["items"] = new List<ParaObject>(0);
        		map["totalHits"] = 0;
        	}
        	return map;
        }

        /////////////////////////////////////////////
        //				 LINKS
        /////////////////////////////////////////////
        
        /// <summary>
        /// Count the total number of links between this object and another type of object.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="type2">the other type of object</param>
        /// <returns>the number of links for the given object</returns>
        public long countLinks(ParaObject obj, string type2)
        {
            if (obj == null || obj.id == null || type2 == null)
            {
                return 0L;
            }
            var paramz = new Dictionary<string, object>();
            paramz["count"] = "true";
            var pager = new Pager();
            string url = obj.getObjectURI() + "/links/" + type2;
            getItems(getEntity(invokeGet(url, paramz), true), pager);
        	return pager.count;
        }
        
        /// <summary>
        /// Returns all objects linked to the given one. Only applicable to many-to-many relationships.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="type2">type of linked objects to search for</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of linked objects</returns>
        public List<ParaObject> getLinkedObjects(ParaObject obj, string type2, params Pager[] pager)
        {
            if (obj == null || obj.id == null || type2 == null)
            {
                return new List<ParaObject>(0);
            }
            string url = obj.getObjectURI() + "/links/" + type2;
            return getItems(getEntity(invokeGet(url, pagerToParams(pager)), true), pager);
    	}

        /// <summary>
        /// Searches through all linked objects in many-to-many relationships.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="type2">type of linked objects to search for</param>
        /// <param name="field">the name of the field to target (within a nested field "nstd")</param>
        /// <param name="query">a query string</param> 
        /// <param name="pager">a Pager</param>
        /// <returns>a list of linked objects</returns>
        public List<ParaObject> findLinkedObjects(ParaObject obj, string type2, string field, string query, 
            params Pager[] pager)
        {
            if (obj == null || obj.id == null || type2 == null)
            {
                return new List<ParaObject>(0);
            }
            var paramz = new Dictionary<string, object>();
            paramz["field"] = field;
            paramz["q"] = string.IsNullOrEmpty(query) ? "*" : query;
            paramz.Concat(pagerToParams(pager));
            string url = obj.getObjectURI() + "/links/" + type2;
            return getItems(getEntity(invokeGet(url, paramz), true), pager);
        }

        /// <summary>
        /// Checks if this object is linked to another.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="type2">the other type</param>
        /// <param name="id2">the other id</param>
        /// <returns>true if the two are linked</returns>
        public bool isLinked(ParaObject obj, string type2, string id2)
        {
            if (obj == null || obj.id == null || type2 == null || id2 == null)
            {
                return false;
            }
            string url = obj.getObjectURI() + "/links/" + type2 + "/" + id2;
            return bool.Parse((string) getEntity(invokeGet(url, null), true));
    	}
        
        /// <summary>
        /// Checks if a given object is linked to this one.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="toObj">the other object</param>
        /// <returns>true if linked</returns>
        public bool isLinked(ParaObject obj, ParaObject toObj)
        {
            if (obj == null || obj.id == null || toObj == null || toObj.id == null)
            {
                return false;
            }
            return isLinked(obj, toObj.type, toObj.id);
        }
        
        /// <summary>
        /// Links an object to this one in a many-to-many relationship.
        /// Only a link is created. Objects are left untouched.
        /// The type of the second object is automatically determined on read.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="id2">link to the object with this id</param>
        /// <returns>the id of the {@link com.erudika.para.core.Linker} object that is created</returns>
        public string link(ParaObject obj, string id2)
        {
            if (obj == null || obj.id == null || id2 == null)
            {
                return null;
            }
            string url = obj.getObjectURI() + "/links/" + id2;
            return (string) getEntity(invokePost(url, null), true);
    	}
        
        /// <summary>
        /// Unlinks an object from this one.
        /// Only a link is deleted. Objects are left untouched.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="type2">the other type</param>
        /// <param name="id2">the other id</param>
        public void unlink(ParaObject obj, string type2, string id2)
        {
            if (obj == null || obj.id == null || type2 == null || id2 == null)
            {
                return;
            }
            string url = obj.getObjectURI() + "/links/" + type2 + "/" + id2;
            invokeDelete(url, null);
        }
        
        /// <summary>
        /// Unlinks all objects that are linked to this one.
        /// Only Linker objects are deleted, other objects are left untouched.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        public void unlinkAll(ParaObject obj)
        {
            if (obj == null || obj.id == null)
            {
                return;
            }
            string url = obj.getObjectURI() + "/links";
            invokeDelete(url, null);
        }
        
        /// <summary>
        /// Count the total number of child objects for this object.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="type2">the type of the other object</param>
        /// <returns>the number of links</returns>
        public long countChildren(ParaObject obj, string type2)
        {
            if (obj == null || obj.id == null || type2 == null)
            {
                return 0L;
            }
            var paramz = new Dictionary<string, object>();
            paramz["count"] = "true";
            paramz["childrenonly"] = "true";
            var pager = new Pager();
            string url = obj.getObjectURI() + "/links/" + type2;
            getItems(getEntity(invokeGet(url, paramz), true), pager);
        	return pager.count;
        }

        /// <summary>
        /// Returns all child objects linked to this object.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="type2">the type of children to look for</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of ParaObject in a one-to-many relationship with this object</returns>
        public List<ParaObject> getChildren(ParaObject obj, string type2, params Pager[] pager)
        {
            if (obj == null || obj.id == null || type2 == null)
            {
                return new List<ParaObject>(0);
            }
            var paramz = new Dictionary<string, object>();
            paramz["childrenonly"] = "true";
            paramz.Concat(pagerToParams(pager));
            string url = obj.getObjectURI() + "/links/" + type2;
            return getItems(getEntity(invokeGet(url, paramz), true), pager);
        }
        
        /// <summary>
        /// Returns all child objects linked to this object.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="type2">the type of children to look for</param>
        /// <param name="field">the field name to use as filter</param>
        /// <param name="term">the field value to use as filter</param>
        /// <param name="pager">a Pager</param>
        /// <returns>a list of ParaObject in a one-to-many relationship with this object</returns>
        public List<ParaObject> getChildren(ParaObject obj, string type2, string field, string term,
                params Pager[] pager)
        {
            if (obj == null || obj.id == null || type2 == null)
            {
                return new List<ParaObject>(0);
            }
            var paramz = new Dictionary<string, object>();
            paramz["childrenonly"] = "true";
            paramz["field"] = field;
            paramz["term"] = term;
            paramz.Concat(pagerToParams(pager));
            string url = obj.getObjectURI() + "/links/" + type2;
            return getItems(getEntity(invokeGet(url, paramz), true), pager);
        }

        /// <summary>
        /// Search through all child objects. Only searches child objects directly
        /// connected to this parent via the {@code parentid} field.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="type2">the type of children to look for</param>
        /// <param name="query">a query string</param> 
        /// <param name="pager">a Pager</param>
        /// <returns>a list of ParaObject in a one-to-many relationship with this object</returns>
        public List<ParaObject> findChildren(ParaObject obj, string type2, string query, params Pager[] pager)
        {
            if (obj == null || obj.id == null || type2 == null)
            {
                return new List<ParaObject>(0);
            }
            var paramz = new Dictionary<string, object>();
            paramz["childrenonly"] = "true";
            paramz["q"] = string.IsNullOrEmpty(query) ? "*" : query;
            paramz.Concat(pagerToParams(pager));
            string url = obj.getObjectURI() + "/links/" + type2;
            return getItems(getEntity(invokeGet(url, paramz), true), pager);
        }
        
        /// <summary>
        /// Deletes all child objects permanently.
        /// </summary>
        /// <param name="obj">the object to execute this method on</param>
        /// <param name="type2">the children's type.</param>
        public void deleteChildren(ParaObject obj, string type2)
        {
            if (obj == null || obj.id == null || type2 == null)
            {
                return;
            }
            var paramz = new Dictionary<string, object>();
            paramz["childrenonly"] = "true";
            string url = obj.getObjectURI() + "/links/" + type2;
            invokeDelete(url, paramz);
        }

        ///////////////////////////////////////////////
        ////				 UTILS
        ///////////////////////////////////////////////
        
        /// <summary>
        /// Generates a new unique id.
        /// </summary>
        /// <returns>a new id</returns>
        public string newId()
        {
            var res = getEntity(invokeGet("utils/newid", null), true);
			return (string) res ?? "";
        }
        
        /// <summary>
        /// Returns the current timestamp.
        /// </summary>
        /// <returns>a long number</returns>
        public long getTimestamp()
        {
            object res = getEntity(invokeGet("utils/timestamp", null), true);
            long timestamp = 0;
        	if (res != null) long.TryParse((string) res, out timestamp);
            return timestamp;
        }
        
        /// <summary>
        /// Formats a date in a specific format.
        /// </summary>
        /// <param name="format">format the date format</param>
        /// <param name="loc">loc the locale instance</param>
        /// <returns>a formatted date</returns>
        public string formatDate(string format, string loc)
        {
            var paramz = new Dictionary<string, object>();
            paramz["format"] = format;
			paramz["locale"] = string.IsNullOrEmpty(loc) ? null : loc;
            return (string) getEntity(invokeGet("utils/formatdate", paramz), true);
    	}
        
        /// <summary>
        /// Converts spaces to dashes.
        /// </summary>
        /// <param name="str">a string with spaces</param>
        /// <param name="replaceWith">a string to replace spaces with</param>
        /// <returns>a string with dashes</returns>
        public string noSpaces(string str, string replaceWith)
        {
            var paramz = new Dictionary<string, object>();
            paramz["string"] = str;
            paramz["replacement"] = replaceWith;
            return (string) getEntity(invokeGet("utils/nospaces", paramz), true);
        }
        
        /// <summary>
        /// Strips all symbols, punctuation, whitespace and control chars from a string.
        /// </summary>
        /// <param name="str">a dirty string</param>
        /// <returns>a clean string</returns>
        public string stripAndTrim(string str)
        {
            var paramz = new Dictionary<string, object>();
            paramz["string"] = str;
            return (string) getEntity(invokeGet("utils/nosymbols", paramz), true);
	    }
        
        /// <summary>
        /// Converts Markdown to HTML
        /// </summary>
        /// <param name="markdownstring">Markdown</param>
        /// <returns>HTML</returns>
        public string markdownToHtml(string markdownstring)
        {
            var paramz = new Dictionary<string, object>();
            paramz["md"] = markdownstring;
            return (string) getEntity(invokeGet("utils/md2html", paramz), true);
    	}
        
        /// <summary>
        /// Returns the number of minutes, hours, months elapsed for a time delta (milliseconds).
        /// </summary>
        /// <param name="delta">the time delta between two events, in milliseconds</param>
        /// <returns>a string like "5m", "1h"</returns>
        public string approximately(long delta)
        {
            var paramz = new Dictionary<string, object>();
            paramz["delta"] = delta.ToString();
            return (string) getEntity(invokeGet("utils/timeago", paramz), true);
    	}

        /////////////////////////////////////////////
        //				 MISC
        /////////////////////////////////////////////
                
        /// <summary>
        /// Generates a new set of access/secret keys.
        /// Old keys are discarded and invalid after this.
        /// </summary>
        /// <returns>a Dictionary of new credentials</returns>
        public Dictionary<string, string> newKeys()
        {
            var keys = JsonConvert.DeserializeObject<Dictionary<string, string>>((string) 
                getEntity(invokePost("_newkeys", null), true));
            if (keys != null && keys.ContainsKey("secretKey")) {
                secretKey = keys["secretKey"];
            }
            return keys;
        }
        
        /// <summary>
        /// Returns all registered types for this App.
        /// </summary>
        /// <returns>a Dictionary of plural-singular form of all the registered types.</returns>
        public Dictionary<string, string> types()
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>((string) getEntity(invokeGet("_types", null), true));
        }
        
        /// <summary>
        /// Returns a User or an App that is currently authenticated.
        /// </summary>
        /// <returns>User or App object or null</returns>
        public ParaObject me()
        {
        	return (ParaObject) getEntity(invokeGet("_me", null), false);
        }

        /// <summary>
        /// Returns a User or an App that is currently authenticated.
        /// </summary>
        /// <param name="accessToken">a valid JWT access token</param>
        /// <returns>User or App object or null</returns>
        public ParaObject me(string accessToken)
        {
            if (!string.IsNullOrEmpty(accessToken))
            {
                var headers = new Dictionary<string, string>();
                headers["Authorization"] = accessToken.StartsWith("Bearer") ? accessToken : "Bearer " + accessToken;
                IRestResponse res = invokeSignedRequest(Method.GET, getEndpoint(), getFullPath("_me"), headers, null, null);
                return (ParaObject) getEntity(res, false);
            }
            return me();
        }

        /// <summary>
        /// Upvote an object and register the vote in DB.
        /// </summary>
        /// <returns><c>true</c>, if vote was successful, <c>false</c> otherwise.</returns>
        /// <param name="obj">the object to receive +1 votes</param>
        /// <param name="voterid">the userid of the voter</param>
        public bool voteUp(ParaObject obj, string voterid)
        {
            if (obj == null || string.IsNullOrEmpty(voterid))
            {
                return false;
            }
            var entity = new Dictionary<string, object>();
            entity["_voteup"] = voterid;
            return bool.Parse((string) getEntity(invokePatch(obj.type + "/" + obj.id, entity), true));
        }

        /// <summary>
        /// Downvote an object and register the vote in DB.
        /// </summary>
        /// <returns><c>true</c>, if vote was successful, <c>false</c> otherwise.</returns>
        /// <param name="obj">the object to receive +1 votes</param>
        /// <param name="voterid">the userid of the voter</param>
        public bool voteDown(ParaObject obj, string voterid)
        {
            if (obj == null || string.IsNullOrEmpty(voterid))
            {
                return false;
            }
            var entity = new Dictionary<string, object>();
            entity["_votedown"] = voterid;
            return bool.Parse((string) getEntity(invokePatch(obj.type + "/" + obj.id, entity), true));
        }

        /// <summary>
        /// Rebuilds the entire search index.
        /// </summary>
        /// <returns>a response object with properties "tookMillis" and "reindexed"</returns>
        public Dictionary<string, string> rebuildIndex()
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>((string) getEntity(invokePost("_reindex", null), true));
        }

        /// <summary>
        /// Rebuilds the entire search index.
        /// </summary>
        /// <returns>a response object with properties "tookMillis" and "reindexed"</returns>
        /// <param name="destinationIndex">an existing index as destination</param>
        public Dictionary<string, string> rebuildIndex(string destinationIndex)
        {
            var paramz = new Dictionary<string, object>();
            paramz["destinationIndex"] = destinationIndex;
            IRestResponse res = invokeSignedRequest(Method.POST, getEndpoint(), getFullPath("_reindex"), null, paramz, null);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>((string) getEntity(res, true));
        }

		/////////////////////////////////////////////
		//		 Validation Constraints
		/////////////////////////////////////////////

		/// <summary>
		/// Returns the validation constraints Dictionary.
		/// </summary>
		/// <returns>a Dictionary containing all validation constraints.</returns>
		public Dictionary<string, object> validationConstraints()
		{
			return JsonConvert.DeserializeObject<Dictionary<string, object>>((string) getEntity(invokeGet("_constraints", null), true));
		}

		/// <summary>
		/// Returns the validation constraints Dictionary.
		/// </summary>
		/// <param name="type">a type</param>
		/// <returns>a Dictionary containing all validation constraints.</returns>
		public Dictionary<string, Dictionary<string, Dictionary<string, object>>> validationConstraints(string type)
		{
			return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, object>>>>
				((string) getEntity(invokeGet("_constraints/" + type, null), true));
		}

		/// <summary>
		/// Add a new constraint for a given field.
		/// </summary>
		/// <param name="type">a type</param>
		/// <param name="field">a field name</param>
		/// <param name="c">the constraint</param>
		/// <returns>a Dictionary containing all validation constraints for this type.</returns>
		public Dictionary<string, Dictionary<string, Dictionary<string, object>>> addValidationConstraint(string type, string field, Constraint c)
		{
			return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, object>>>> 
				((string) getEntity(invokePut("_constraints/" + type + "/" + field + "/" + c.name, c.payload), true));
		}

		/// <summary>
		/// Removes a validation constraint for a given field.
		/// </summary>
		/// <param name="type">a type</param>
		/// <param name="field">a field name</param>
		/// <param name="constraintName">the name of the constraint to remove</param>
		/// <returns>a Dictionary containing all validation constraints for this type.</returns>
		public Dictionary<string, object> removeValidationConstraint(string type, string field, string constraintName)
		{
			return JsonConvert.DeserializeObject<Dictionary<string, object>>
				((string) getEntity(invokeDelete("_constraints/" + type + "/" + field + "/" + constraintName, null), true));
		}

        /////////////////////////////////////////////
        //       Resource Permissions
        /////////////////////////////////////////////
    
        /// <summary>
        /// Returns the permissions for all subjects and resources for current app.
        /// </summary>
        /// <returns>a map of subject ids to resource names to a list of allowed methods</returns>
        public Dictionary<string, Dictionary<string, List<string>>> resourcePermissions() {
                return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<string>>>>
                    ((string) getEntity(invokeGet("_permissions", null), true));
        }

        /// <summary>
        /// Returns only the permissions for a given subject (user) of the current app.
        /// </summary>
        /// <returns>a map of subject ids to resource names to a list of allowed methods</returns>
        /// <param name="subjectid">the subject id (user id)</param>
        public Dictionary<string, Dictionary<string, List<string>>> resourcePermissions(string subjectid) {
            return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<string>>>>
                ((string) getEntity(invokeGet("_permissions/" + subjectid, null), true));
        }

        /// <summary>
        /// Grants a permission to a subject that allows them to call the specified HTTP methods on a given resource.
        /// </summary>
        /// <returns>a map of the permissions for this subject id</returns>
        /// <param name="subjectid">subject id (user id)</param>
        /// <param name="resourcePath">resource path or object type</param>
        /// <param name="permission">a set of HTTP methods</param>
        public Dictionary<string, Dictionary<string, List<string>>> grantResourcePermission(string subjectid, string resourcePath,
            string[] permission) {
            return grantResourcePermission(subjectid, resourcePath, permission, false);
        }

        /// <summary>
        /// Grants a permission to a subject that allows them to call the specified HTTP methods on a given resource.
        /// </summary>
        /// <returns>a map of the permissions for this subject id</returns>
        /// <param name="subjectid">subject id (user id)</param>
        /// <param name="resourcePath">resource path or object type</param>
        /// <param name="permission">a set of HTTP methods</param>
        /// <param name="allowGuestAccess">if true - all unauthenticated requests will go through, 'false' by default.</param>
        public Dictionary<string, Dictionary<string, List<string>>> grantResourcePermission(string subjectid, string resourcePath,
            string[] permission, bool allowGuestAccess) {
            if (string.IsNullOrEmpty(subjectid) || string.IsNullOrEmpty(resourcePath) || permission == null) {
                return new Dictionary<string, Dictionary<string, List<string>>>();
            }
            if (allowGuestAccess && "*".Equals(subjectid)) {
                var arr = new string[permission.Length + 1];
                permission.CopyTo(arr, 0);
                arr[permission.Length] = "?";
                permission = arr;
            }
            resourcePath = Uri.EscapeDataString(resourcePath);
            return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<string>>>>
                ((string) getEntity(invokePut("_permissions/" + subjectid + "/" + resourcePath, permission), true));
        }

        /// <summary>
        /// Revokes a permission for a subject, meaning they no longer will be able to access the given resource.
        /// </summary>
        /// <returns>a map of the permissions for this subject id</returns>
        /// <param name="subjectid">subject id (user id)</param>
        /// <param name="resourcePath">resource path or object type</param>
        public Dictionary<string, Dictionary<string, List<string>>> revokeResourcePermission(string subjectid, string resourcePath) {
            if (string.IsNullOrEmpty(subjectid) || string.IsNullOrEmpty(resourcePath)) {
                return new Dictionary<string, Dictionary<string, List<string>>>();
            }
            resourcePath = Uri.EscapeDataString(resourcePath);
            return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<string>>>>
                ((string) getEntity(invokeDelete("_permissions/" + subjectid + "/" + resourcePath, null), true));
        }

        /// <summary>
        /// Revokes all permission for a subject.
        /// </summary>
        /// <returns>a map of the permissions for this subject id</returns>
        /// <param name="subjectid">subject id (user id)</param>
        public Dictionary<string, Dictionary<string, List<string>>> revokeAllResourcePermissions(string subjectid) {
            if (string.IsNullOrEmpty(subjectid)) {
                return new Dictionary<string, Dictionary<string, List<string>>>();
            }
            return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<string>>>>
                ((string) getEntity(invokeDelete("_permissions/" + subjectid, null), true));
        }

        /// <summary>
        /// Checks if a subject is allowed to call method X on resource Y.
        /// </summary>
        /// <returns><c>true</c>, if allowed, <c>false</c> otherwise.</returns>
        /// <param name="subjectid">subject id</param>
        /// <param name="resourcePath">resource path or object type</param>
        /// <param name="httpMethod">HTTP method name</param>
        public bool isAllowedTo(string subjectid, string resourcePath, string httpMethod) {
            if (string.IsNullOrEmpty(subjectid) || string.IsNullOrEmpty(resourcePath) || string.IsNullOrEmpty(httpMethod)) {
                return false;
            }
            resourcePath = Uri.EscapeDataString(resourcePath);
            string url = "_permissions/" + subjectid + "/" + resourcePath + "/" + httpMethod;
            return bool.Parse((string) getEntity(invokeGet(url, null), true));
        }

        /////////////////////////////////////////////
        //       Resource Permissions
        /////////////////////////////////////////////

        /// <summary>
        /// Returns the map containing app-specific settings.
        /// </summary>
        /// <returns>a map.</returns>
        public Dictionary<string, object> appSettings() {
            return JsonConvert.DeserializeObject<Dictionary<string, object>>
                ((string) getEntity(invokeGet("_settings", null), true));
        }

        /// <summary>
        /// Returns the value of a specific app setting (property).
        /// </summary>
        /// <returns>a map with a single entry {value: value}.</returns>
        /// <param name="key">a key</param>
        public Dictionary<string, object> appSettings(string key) {
            if (string.IsNullOrEmpty (key)) {
                return appSettings();
            }
            return JsonConvert.DeserializeObject<Dictionary<string, object>>
                ((string) getEntity(invokeGet("_settings/" + key, null), true));
        }

        /// <summary>
        /// Adds or overwrites an app-specific setting.
        /// </summary>
        /// <param name="key">a key</param>
        /// <param name="value">a value</param>
        public void addAppSetting(string key, object value) {
            if (!string.IsNullOrEmpty(key) && value != null) {
                invokePut("_settings/" + key, new Dictionary<string, object> { { "value", value } });
            }
        }

        /// <summary>
        /// Overwrites all app-specific settings.
        /// </summary>
        /// <param name="settings">a key-value map of properties</param>
        public void setAppSettings(Dictionary<string, object> settings)
        {
            if (settings != null)
            {
                invokePut("_settings", settings);
            }
        }

        /// <summary>
        /// Removes an app-specific setting.
        /// </summary>
        /// <param name="key">a key</param>
        public void removeAppSetting(string key) {
            if (!string.IsNullOrEmpty(key)) {
                invokeDelete("_settings/" + key, null);
            }
        }

        /////////////////////////////////////////////
        //              Access Tokens
        /////////////////////////////////////////////
    
        /// <summary>
        /// Takes an identity provider access token and fetches the user data from that provider.
        /// A new {@link  User} object is created if that user doesn't exist.
        /// Access tokens are returned upon successful authentication using one of the SDKs from
        /// Facebook, Google, Twitter, etc.
        /// <b>Note:</b> Twitter uses OAuth 1 and gives you a token and a token secret.
        /// <b>You must concatenate them like this: <code>{oauth_token}:{oauth_token_secret}</code> and
        /// use that as the provider access token.</b>
        /// </summary>
        /// <returns>a user ParaObject or null if something failed</returns>
        /// <param name="provider">identity provider, e.g. 'facebook', 'google'...</param>
        /// <param name="providerToken">access token from a provider like Facebook, Google, Twitter</param>
        public ParaObject signIn(string provider, string providerToken, bool rememberJWT) {
            if (!string.IsNullOrEmpty(provider) && !string.IsNullOrEmpty(providerToken)) {
                var credentials = new Dictionary<string, string>();
                credentials["appid"] = accessKey;
                credentials["provider"] = provider;
                credentials["token"] = providerToken;
                var res = getEntity(invokePost(JWT_PATH, credentials), true);
                var result = (res == null) ? null : JsonConvert.DeserializeObject
                    <Dictionary<string, Dictionary<string, object>>>((string) res);
                if (result != null && result.ContainsKey("user") && result.ContainsKey("jwt")) {
                    var jwtData = result["jwt"];
                    if (rememberJWT)
                    {
                        tokenKey = (string)jwtData["access_token"];
                        tokenKeyExpires = (long)(jwtData["expires"] ?? -1);
                        tokenKeyNextRefresh = (long)(jwtData["refresh"] ?? -1);
                    }
                    var user = new ParaObject();
                    user.setFields(result["user"]);
                    return user;
                }
                else
                {
                    clearAccessToken();
                }
            }
            return null;
        }

        /// <summary>
        /// #see cref="ParaClient.signIn(string provider, string providerToken, bool rememberJWT)"/>
        /// </summary>
        /// <returns>User, App or null</returns>
        /// <param name="provider">Provider.</param>
        /// <param name="providerToken">Provider token.</param>
        public ParaObject signIn(string provider, string providerToken)
        {
            return signIn(provider, providerToken, true);
        }

        /// <summary>
        /// Clears the JWT access token but token is not revoked.
        /// Tokens can be revoked globally per user with revokeAllTokens().
        /// </summary>
        public void signOut() {
            clearAccessToken();
        }

        /// <summary>
        /// Refreshes the JWT access token. This requires a valid existing token.
        /// Call signIn() first.
        /// </summary>
        /// <returns><c>true</c>, if token was refreshed, <c>false</c> otherwise.</returns>
        protected bool refreshToken() {
            long now = CurrentTimeMillis();
            bool notExpired = tokenKeyExpires > 0 && tokenKeyExpires > now;
            bool canRefresh = tokenKeyNextRefresh > 0 &&
                (tokenKeyNextRefresh < now || tokenKeyNextRefresh > tokenKeyExpires);
            // token present and NOT expired
            if (tokenKey != null && notExpired && canRefresh) {
                var res = getEntity(invokeGet(JWT_PATH, null), true);
                var result = (res == null) ? null : JsonConvert.DeserializeObject
                    <Dictionary<string, Dictionary<string, object>>>((string) res);
                if (result != null && result.ContainsKey("user") && result.ContainsKey("jwt")) {
                    var jwtData = result["jwt"];
                    tokenKey = (string) jwtData["access_token"];
                    tokenKeyExpires = (long) (jwtData["expires"] ?? -1);
                    tokenKeyNextRefresh = (long) (jwtData["refresh"] ?? -1);
                    return true;
                }
                else
                {
                    clearAccessToken();
                }
            }
            return false;
        }

        /// <summary>
        /// Revokes all user tokens for a given user id.
        /// This would be equivalent to "logout everywhere".
        /// <b>Note:</b> Generating a new API secret on the server will also invalidate all client tokens.
        /// Requires a valid existing token.
        /// </summary>
        /// <returns><c>true</c>, if all token was revoked, <c>false</c> otherwise.</returns>
        public bool revokeAllTokens() {
            return getEntity(invokeDelete(JWT_PATH, null), true) != null;
        }
    }

    public class ParaRequest : AmazonWebServiceRequest
    {
        
    }

    public class ParaConfig : ClientConfig
    {
        public override string RegionEndpointServiceName
        {
            get
            {
                return "para";
            }
        }

        public override string ServiceVersion
        {
            get
            {
                return "2015-07-07";
            }
        }

        public override string UserAgent
        {
            get
            {
                return "ParaClient for .NET";
            }
        }
    }    
}
