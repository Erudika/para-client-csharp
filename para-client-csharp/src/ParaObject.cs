/*
 * Copyright 2013-2018 Erudika. https://erudika.com
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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Para.Client
{
    /// <summary>
    /// The core domain class. All Para objects extend it.
    /// </summary>
    [Serializable]
    public class ParaObject
    {
        /// <summary>
        /// The id of an object. Usually an autogenerated unique string of numbers.
        /// </summary>
        public string id { get; set; }
        /// <summary>
        /// The time when the object was created, in milliseconds (Java-style Unix timestamp).
        /// </summary>
        public long timestamp { get; set; }
        /// <summary>
        /// The type of the object. 
        /// </summary>
        public string type { get; set; }
        /// <summary>
        /// The application name. Added to support multiple separate apps.
        /// Every object must belong to an app.
        /// </summary>
        public string appid { get; set; }
        /// <summary>
        /// The id of the parent object.
        /// </summary>
        public string parentid { get; set; }
        /// <summary>
        /// The id of the user who created this. Should point to a {@link User} id.
        /// </summary>
        public string creatorid { get; set; }
        /// <summary>
        /// The last time this object was updated. Timestamp in milliseconds.
        /// </summary>
        public long updated { get; set; }
        /// <summary>
        /// The name of the object. Can be anything.
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// The tags associated with this object. Tags must not be null or empty.
        /// </summary>
        public List<string> tags { get; set; }
        /// <summary>
        /// Returns the total sum of all votes for this object.
        /// </summary>
        public int votes { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this object is stored in DB.
        /// </summary>
        /// <value><c>true</c> if stored; otherwise, <c>false</c>.</value>
        public bool stored { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this object is indexed.
        /// </summary>
        /// <value><c>true</c> if indexed; otherwise, <c>false</c>.</value>
        public bool indexed { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this object is cached.
        /// </summary>
        /// <value><c>true</c> if cached; otherwise, <c>false</c>.</value>
        public bool cached { get; set; }

        [JsonExtensionData]
        public readonly Dictionary<string, object> properties = new Dictionary<string, object>();
        
        public ParaObject() : this(null, null)
        {
        }

        public ParaObject(string id) : this(id, null)
        {
        }

        public ParaObject(string id, string type)
        {
            this.id = id;
            this.type = type;
            if (string.IsNullOrEmpty(type)) this.type = "sysprop";
            this.votes = 0;
            this.name = "ParaObject";
            this.stored = true;
            this.indexed = true;
            this.cached = true;
        }

        /// <summary>
        /// The plural name of the object. For example: user - users.
        /// </summary>
        /// <returns>a string</returns>
        public string getPlural()
        {
            return (this.type == null) ? this.type :
						(this.type.Last() == 's') ? this.type + "es" :
						(this.type.Last() == 'y') ? this.type.Remove(this.type.Length - 1, 1) + "ies" : this.type + "s";
        }

        /// <summary>
        /// The URI of this object. For example: /users/123.
        /// </summary>
        /// <returns>the URI string</returns>
        public string getObjectURI()
        {
		    string def = "/" + getPlural();
            return (this.id != null) ? def + "/" + this.id : def;
        }

        public object this[string name]
        {
            get
            {
                if (properties.ContainsKey(name))
                {
                    return properties[name];
                }
                return null;
            }
            set
            {
                properties[name] = value;
            }
        }

        /// <summary>
        /// Populates this object with data from a Dictionary.
        /// </summary>
        /// <param name="map">a dictionary of data</param>
        /// <returns>this ParaObject</returns>
        public ParaObject setFields(Dictionary<string, object> map)
        {
            if (map != null)
            {
                foreach (KeyValuePair<string, object> entry in map) {
                    try
                    {
                        GetType().GetProperty(entry.Key).SetValue(this, entry.Value, null);
                    }
                    catch
                    {
                        this[entry.Key] = entry.Value;
                    }
                }
            }
            return this;
        }

        /// <summary>
        /// Returns the JSON serialization of this object.
        /// </summary>
        /// <returns>a JSON string</returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
    