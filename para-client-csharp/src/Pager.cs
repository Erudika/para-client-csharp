﻿/*
 * Copyright 2013-2022 Erudika. https://erudika.com
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
    /// This class stores pagination data. It limits the results for queries in the DAO
    /// and Search objects and also counts the total number of results that are returned.
    /// </summary>
    public class Pager
    {
        public long page { get; set; }
        public long count { get; set; }
        public string sortby { get; set; }
        public bool desc { get; set; }
        public int limit { get; set; }
        public string name { get; set; }
        public string lastKey { get; set; }
        public List<string> select { get; set; }

        public Pager() : this(1, null, true, 30)
        {
        }

        public Pager(int limit) : this(1, null, true, limit)
        {
        }

        public Pager(int page, int limit) : this(page, null, true, limit)
        {
        }

        public Pager(int page, string sortby, bool desc, int limit)
        {
            this.count = 0;
            this.page = page;
            this.sortby = sortby;
            this.desc = desc;
            this.limit = limit;
            this.select = null;
        }
    }
}
