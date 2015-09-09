/*
 * Copyright 2013-2015 Erudika. http://erudika.com
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
    [Serializable]
    public class ParaObject
    {        
        public string id { get; set; }
        public long timestamp { get; set; }
        public string type { get; set; }
        public string appid { get; set; }
        public string parentid { get; set; }
        public string creatorid { get; set; }
        public long updated { get; set; }
        public string name { get; set; }
        public List<string> tags { get; set; }
        public int votes { get; set; }

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
        }

        public string getPlural()
        {
            return (this.type == null) ? this.type :
						(this.type.Last() == 's') ? this.type + "es" :
						(this.type.Last() == 'y') ? this.type.Remove(this.type.Length - 1, 1) + "ies" : this.type + "s";
        }

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

        public ParaObject setFields(Dictionary<string, object> map)
        {
            if (map != null)
            {
                foreach (KeyValuePair<string, object> entry in map) {
                    try
                    {
                        GetType().GetProperty(entry.Key).SetValue(this, entry.Value, null);
                    }
                    catch (Exception e)
                    {
                        this[entry.Key] = entry.Value;
                    }
                }
            }
            return this;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
    