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
using System.Collections.Generic;

namespace Para.Client
{
    public class Constraint
    {

        public string name { get; set; }
        public Dictionary<string, object> payload { get; set; }

        public Constraint(string name, Dictionary<string, object> payload)
        {
            this.name = name;
            this.payload = payload;
        }

        public static Constraint required()
        {
            return new Constraint("required", new Dictionary<string, object> { { "message", "messages.required" } });
        }

        public static Constraint min(int min)
        {
            return new Constraint("min", new Dictionary<string, object> {
                { "value", min },
                { "message", "messages.min" }
            });
        }

        public static Constraint max(int max)
        {
            return new Constraint("max", new Dictionary<string, object> {
                { "value", max },
                { "message", "messages.max" }
            });
        }

        public static Constraint size(int min, int max)
        {
            return new Constraint("size", new Dictionary<string, object> {
                { "min", min },
                { "max", max },
                { "message", "messages.size" }
            });
        }

        public static Constraint digits(int i, int f)
        {
            return new Constraint("digits", new Dictionary<string, object> {
                { "integer", i },
                { "fraction", f },
                { "message", "messages.digits" }
            });
        }

        public static Constraint pattern(string regex)
        {
            return new Constraint("pattern", new Dictionary<string, object> {
                { "value", regex },
                { "message", "messages.pattern" }
            });
        }

        public static Constraint email()
        {
            return new Constraint("email", new Dictionary<string, object> { { "message", "messages.email" } });
        }

        public static Constraint falsy()
        {
            return new Constraint("false", new Dictionary<string, object> { { "message", "messages.false" } });
        }

        public static Constraint truthy()
        {
            return new Constraint("true", new Dictionary<string, object> { { "message", "messages.true" } });
        }

        public static Constraint future()
        {
            return new Constraint("future", new Dictionary<string, object> { { "message", "messages.future" } });
        }

        public static Constraint past()
        {
            return new Constraint("past", new Dictionary<string, object> { { "message", "messages.past" } });
        }

        public static Constraint url()
        {
            return new Constraint("url", new Dictionary<string, object> { { "message", "messages.url" } });
        }        
    }
}
