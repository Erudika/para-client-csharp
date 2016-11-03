/*
 * Copyright 2013-2015 Erudika. https://erudika.com
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
    /// <summary>
    /// Represents a validation constraint.
    /// </summary>
    public class Constraint
    {
        /// <summary>
        /// The constraint name.
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// The payload (a map)
        /// </summary>
        public Dictionary<string, object> payload { get; set; }

        public Constraint(string name, Dictionary<string, object> payload)
        {
            this.name = name;
            this.payload = payload;
        }

        /// <summary>
        /// The 'required' constraint - marks a field as required.
        /// </summary>
        /// <returns>constraint</returns>
        public static Constraint required()
        {
            return new Constraint("required", new Dictionary<string, object> { { "message", "messages.required" } });
        }

        /// <summary>
        /// The 'min' constraint - field must contain a number larger than or equal to min.
        /// </summary>
        /// <param name="min">the minimum value</param>
        /// <returns>constraint</returns>
        public static Constraint min(int min)
        {
            return new Constraint("min", new Dictionary<string, object> {
                { "value", min },
                { "message", "messages.min" }
            });
        }

        /// <summary>
        /// The 'max' constraint - field must contain a number smaller than or equal to max.
        /// </summary>
        /// <param name="max">the maximum value</param>
        /// <returns>constraint</returns>
        public static Constraint max(int max)
        {
            return new Constraint("max", new Dictionary<string, object> {
                { "value", max },
                { "message", "messages.max" }
            });
        }

        /// <summary>
        /// The 'size' constraint - field must be a String, Object or Array 
        /// with a given minimum and maximum length.
        /// </summary>
        /// <param name="min">the minimum length</param>
        /// <param name="max">the maximum length</param>
        /// <returns>constraint</returns>
        public static Constraint size(int min, int max)
        {
            return new Constraint("size", new Dictionary<string, object> {
                { "min", min },
                { "max", max },
                { "message", "messages.size" }
            });
        }

        /// <summary>
        /// The 'digits' constraint - field must be a Number or String containing digits where the
        /// number of digits in the integral part is limited by 'integer', and the
        /// number of digits for the fractional part is limited
        /// </summary>
        /// <param name="i">the max number of digits for the integral part</param>
        /// <param name="f">the max number of digits for the fractional part</param>
        /// <returns>constraint</returns>
        public static Constraint digits(int i, int f)
        {
            return new Constraint("digits", new Dictionary<string, object> {
                { "integer", i },
                { "fraction", f },
                { "message", "messages.digits" }
            });
        }

        /// <summary>
        /// The 'pattern' constraint - field must contain a value matching a regular expression.
        /// </summary>
        /// <param name="regex">a regular expression</param>
        /// <returns>constraint</returns>
        public static Constraint pattern(string regex)
        {
            return new Constraint("pattern", new Dictionary<string, object> {
                { "value", regex },
                { "message", "messages.pattern" }
            });
        }

        /// <summary>
        /// The 'email' constraint - field must contain a valid email.
        /// </summary>
        /// <returns>constraint</returns>
        public static Constraint email()
        {
            return new Constraint("email", new Dictionary<string, object> { { "message", "messages.email" } });
        }

        /// <summary>
        /// The 'falsy' constraint - field value must not be equal to 'true'.
        /// </summary>
        /// <returns>constraint</returns>
        public static Constraint falsy()
        {
            return new Constraint("false", new Dictionary<string, object> { { "message", "messages.false" } });
        }

        /// <summary>
        /// The 'truthy' constraint - field value must be equal to 'true'.
        /// </summary>
        /// <returns>constraint</returns>
        public static Constraint truthy()
        {
            return new Constraint("true", new Dictionary<string, object> { { "message", "messages.true" } });
        }

        /// <summary>
        /// The 'future' constraint - field value must be a Date or a timestamp in the future.
        /// </summary>
        /// <returns>constraint</returns>
        public static Constraint future()
        {
            return new Constraint("future", new Dictionary<string, object> { { "message", "messages.future" } });
        }

        /// <summary>
        /// The 'past' constraint - field value must be a Date or a timestamp in the past.
        /// </summary>
        /// <returns>constraint</returns>
        public static Constraint past()
        {
            return new Constraint("past", new Dictionary<string, object> { { "message", "messages.past" } });
        }

        /// <summary>
        /// The 'url' constraint - field value must be a valid URL.
        /// </summary>
        /// <returns>constraint</returns>
        public static Constraint url()
        {
            return new Constraint("url", new Dictionary<string, object> { { "message", "messages.url" } });
        }        
    }
}
