using System;
using Para.Client;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace Para.Client.Tests
{
	[TestFixture]
    public class ParaClientTest
    {

        static ParaClient pc;
        static ParaClient pc2;
        const string catsType = "cat";
        const string dogsType = "dog";
        const string batsType = "bat";

        protected static ParaObject u;
        protected static ParaObject u1;
        protected static ParaObject u2;
        protected static ParaObject t;
        protected static ParaObject s1;
        protected static ParaObject s2;
        protected static ParaObject a1;
        protected static ParaObject a2;


        [OneTimeSetUp]
        public static void setUpClass()
        {
            pc = new ParaClient("app:para", "xC2/S0vrq41lYlFliGmKfmuuQBe1ixf2DXbgzbCq0q6TIu6W66uH3g==");
            pc.setEndpoint("http://localhost:8080");
            pc2 = new ParaClient("app:para", null);
            pc2.setEndpoint("http://localhost:8080");
            if (pc.me() == null) {
                throw new Exception("Local Para server must be started before testing.");
            }

            u = new ParaObject("111");
            u.name = "John Doe";
            u.tags = new List<string> { "one", "two", "three" };

            u1 = new ParaObject("222");
            u1.name = "Joe Black";
            u1.tags = new List<string> { "two", "four", "three" };

            u2 = new ParaObject("333");
            u2.name = "Ann Smith";
            u2.tags = new List<string> { "four", "five", "three" };

            t = new ParaObject("tag:test", "tag");
            t["count"] = 3;
            t["tag"] = "test";

            a1 = new ParaObject("adr1", "address");
            a1.name = "Place 1";
            a1["address"] = "NYC";
            a1["country"] = "US";
            a1["latlng"] = "40.67,-73.94";
            a1.parentid = u.id;
            a1.creatorid = u.id;

            a2 = new ParaObject("adr2", "address");
            a2.name = "Place 2";
            a2["address"] = "NYC";
            a2["country"] = "US";
            a2["latlng"] = "40.69,-73.95";
            a2.parentid = t.id;
            a2.creatorid = t.id;

            s1 = new ParaObject("s1");
            s1["text"] = "This is a little test sentence. Testing, one, two, three.";

            s2 = new ParaObject("s2");
            s2["text"] = "We are testing this thing. This sentence is a test. One, two.";

            pc.createAll(new List<ParaObject> { u, u1, u2, t, s1, s2, a1, a2 });
            Thread.Sleep(1000);
        }

        [Test]
        public void testCRUD()
        {
            Assert.IsNull(pc.create(null));
            ParaObject t1 = pc.create(new ParaObject("test1", "tag"));
            t1["tag"] = "test1";
            Assert.IsNotNull(t1);

            Assert.IsNull(pc.read(null, null));
            Assert.IsNull(pc.read("", ""));

            ParaObject trID = pc.read(t1.id);
            Assert.IsNotNull(trID);
            Assert.IsNotNull(trID.timestamp);
            Assert.AreEqual(t1["tag"], trID["tag"]);

            ParaObject tr = pc.read(t1.type, t1.id);
            Assert.IsNotNull(tr);
            Assert.IsNotNull(tr.timestamp);
            Assert.AreEqual(t1["tag"], tr["tag"]);

            tr["count"] = (Int64)15;
            ParaObject tu = pc.update(tr);
            Assert.IsNull(pc.update(new ParaObject("null")));
            Assert.IsNotNull(tu);
            Assert.AreEqual(tu["count"], tr["count"]);
            Assert.IsNotNull(tu.updated);

            ParaObject s = new ParaObject();
            s.type = dogsType;
            s["foo"] = "bark!";
            s = pc.create(s);

            ParaObject dog = pc.read(dogsType, s.id);
            Assert.IsNotNull(dog["foo"]);
            Assert.AreEqual("bark!", dog["foo"]);

            pc.delete(t1);
            pc.delete(dog);
            Assert.IsNull(pc.read(tr.type, tr.id));
        }

        [Test]
        public void testBatchCRUD()
        {
            List<ParaObject> dogs = new List<ParaObject>();
            for (int i = 0; i < 3; i++)
            {
                ParaObject s = new ParaObject();
                s.type = dogsType;
                s["foo"] = "bark!";
                dogs.Add(s);
            }

            Assert.IsTrue(pc.createAll(null).Count == 0);
            List<ParaObject> l1 = pc.createAll(dogs);
            Assert.AreEqual(3, l1.Count);
            Assert.IsNotNull(l1[0].id);

            Assert.IsTrue(pc.readAll(null).Count == 0);
            List<string> nl = new List<string>(3);
            Assert.IsTrue(pc.readAll(nl).Count == 0);
            nl.Add(l1[0].id);
            nl.Add(l1[1].id);
            nl.Add(l1[2].id);
            List<ParaObject> l2 = pc.readAll(nl);
            Assert.AreEqual(3, l2.Count);
            Assert.AreEqual(l1[0].id, l2[0].id);
            Assert.AreEqual(l1[1].id, l2[1].id);
            Assert.IsNotNull(l2[0]["foo"]);
            Assert.AreEqual("bark!", l2[0]["foo"]);

            Assert.IsTrue(pc.updateAll(null).Count == 0);

            ParaObject part1 = new ParaObject(l1[0].id);
            ParaObject part2 = new ParaObject(l1[1].id);
            ParaObject part3 = new ParaObject(l1[2].id);
            part1.type = dogsType;
            part2.type = dogsType;
            part3.type = dogsType;

            part1["custom"] = "prop";
            part1.name = "NewName1";
            part2.name = "NewName2";
            part3.name = "NewName3";

            List<ParaObject> l3 = pc.updateAll(new List<ParaObject> { part1, part2, part3 });

            Assert.IsNotNull(l3[0]["custom"]);
            Assert.AreEqual(dogsType, l3[0].type);
            Assert.AreEqual(dogsType, l3[1].type);
            Assert.AreEqual(dogsType, l3[2].type);

            Assert.AreEqual(part1.name, l3[0].name);
            Assert.AreEqual(part2.name, l3[1].name);
            Assert.AreEqual(part3.name, l3[2].name);

            pc.deleteAll(nl);
            Thread.Sleep(1000);

            List<ParaObject> l4 = pc.list(dogsType);
            Assert.IsTrue(l4.Count == 0);

            Assert.IsTrue(((Dictionary<string, object>) pc.getApp()["datatypes"]).ContainsValue(dogsType));
        }

        [Test]
        public void testList()
        {
            List<ParaObject> cats = new List<ParaObject>();
            for (int i = 0; i < 3; i++)
            {
                ParaObject s = new ParaObject(catsType + i);
                s.type = catsType;
                cats.Add(s);
            }
            pc.createAll(cats);
            Thread.Sleep(1000);

            Assert.IsTrue(pc.list(null).Count == 0);
            Assert.IsTrue(pc.list("").Count == 0);

            List<ParaObject> list1 = pc.list(catsType);
            Assert.IsFalse(list1.Count == 0);
            Assert.AreEqual(3, list1.Count);
            Assert.AreEqual(typeof(ParaObject), list1[0].GetType());

            List<ParaObject> list2 = pc.list(catsType, new Pager(2));
            Assert.IsFalse(list2.Count == 0);
            Assert.AreEqual(2, list2.Count);

            List<string> nl = new List<string>(3);
            nl.Add(cats[0].id);
            nl.Add(cats[1].id);
            nl.Add(cats[2].id);
            pc.deleteAll(nl);

            Assert.IsTrue(((Dictionary<string, object>) pc.getApp()["datatypes"]).ContainsValue(catsType));
        }

        [Test]
        public void testSearch()
        {
            Assert.IsNull(pc.findById(null));
            Assert.IsNull(pc.findById(""));
            Assert.IsNotNull(pc.findById(u.id));
            Assert.IsNotNull(pc.findById(t.id));

            Assert.IsTrue(pc.findByIds(null).Count == 0);
            Assert.AreEqual(3, pc.findByIds(new List<string> { u.id, u1.id, u2.id }).Count);

            Assert.IsTrue(pc.findNearby(null, null, 100, 1, 1).Count == 0);
            List<ParaObject> l1 = pc.findNearby(u.type, "*", 10, 40.60, -73.90);
            Assert.IsFalse(l1.Count == 0);

            Assert.IsTrue(pc.findNearby(null, null, 100, 1, 1).Count == 0);
            l1 = pc.findNearby(u.type, "*", 10, 40.60, -73.90);
            Assert.IsFalse(l1.Count == 0);

            Assert.IsTrue(pc.findPrefix(null, null, "").Count == 0);
            Assert.IsTrue(pc.findPrefix("", "null", "xx").Count == 0);
            Assert.IsFalse(pc.findPrefix(u.type, "name", "Ann").Count == 0);

            Assert.IsFalse(pc.findQuery(null, null).Count == 0);
            Assert.IsFalse(pc.findQuery("", "*").Count == 0);
            Assert.AreEqual(2, pc.findQuery(a1.type, "country:US").Count);
            Assert.IsFalse(pc.findQuery(u.type, "Ann*").Count == 0);
            Assert.IsFalse(pc.findQuery(u.type, "Ann*").Count == 0);
            Assert.IsTrue(pc.findQuery(null, "*").Count > 4);

            Pager p = new Pager();
            Assert.AreEqual(0, p.count);
            List<ParaObject> res = pc.findQuery(u.type, "*", p);
            Assert.AreEqual(res.Count, p.count);
            Assert.IsTrue(p.count > 0);

            Assert.IsTrue(pc.findSimilar(t.type, "", null, null).Count == 0);
            Assert.IsTrue(pc.findSimilar(t.type, "", new string[0], "").Count == 0);
            res = pc.findSimilar(s1.type, s1.id, new [] { "properties.text" }, (string) s1["text"]);
            Assert.IsFalse(res.Count == 0);
            Assert.AreEqual(s2.id, res[0].id);

            int i0 = pc.findTagged(u.type, null).Count;
            int i1 = pc.findTagged(u.type, new [] { "two" }).Count;
            int i2 = pc.findTagged(u.type, new [] { "one", "two" }).Count;
            int i3 = pc.findTagged(u.type, new [] { "three" }).Count;
            int i4 = pc.findTagged(u.type, new [] { "four", "three" }).Count;
            int i5 = pc.findTagged(u.type, new [] { "five", "three" }).Count;
            int i6 = pc.findTagged(t.type, new [] { "four", "three" }).Count;

            Assert.AreEqual(0, i0);
            Assert.AreEqual(2, i1);
            Assert.AreEqual(1, i2);
            Assert.AreEqual(3, i3);
            Assert.AreEqual(2, i4);
            Assert.AreEqual(1, i5);
            Assert.AreEqual(0, i6);

            Assert.IsFalse(pc.findTags(null).Count == 0);
            Assert.IsFalse(pc.findTags("").Count == 0);
            Assert.IsTrue(pc.findTags("unknown").Count == 0);
            Assert.IsTrue(pc.findTags((string)t["tag"]).Count >= 1);

            Assert.AreEqual(3, pc.findTermInList(u.type, "id",
                    new List<string> { u.id, u1.id, u2.id, "xxx", "yyy" }).Count);

            // many terms
            Dictionary<string, object> terms = new Dictionary<string, object>();
            //terms.put("type", u.type);
            terms["id"] = u.id;

            Dictionary<string, object> terms1 = new Dictionary<string, object>();
            terms1["type"] = null;
            terms1["id"] = " ";

            Dictionary<string, object> terms2 = new Dictionary<string, object>();
            terms2[" "] = "bad";
            terms2[""] = "";

            Assert.AreEqual(1, pc.findTerms(u.type, terms, true).Count);
            Assert.IsTrue(pc.findTerms(u.type, terms1, true).Count == 0);
            Assert.IsTrue(pc.findTerms(u.type, terms2, true).Count == 0);

            // single term
            Assert.IsTrue(pc.findTerms(null, null, true).Count == 0);
            Assert.IsTrue(pc.findTerms(u.type, new Dictionary<string, object> { { "", null } }, true).Count == 0);
            Assert.IsTrue(pc.findTerms(u.type, new Dictionary<string, object> { { "", "" } }, true).Count == 0);
            Assert.IsTrue(pc.findTerms(u.type, new Dictionary<string, object> { { "term", null } }, true).Count == 0);
            Assert.IsTrue(pc.findTerms(u.type, new Dictionary<string, object> { { "type", u.type } }, true).Count >= 2);

            Assert.IsTrue(pc.findWildcard(u.type, null, null).Count == 0);
            Assert.IsTrue(pc.findWildcard(u.type, "", "").Count == 0);
            Assert.IsFalse(pc.findWildcard(u.type, "name", "An*").Count == 0);

            Assert.IsTrue(pc.getCount(null) > 4);
            Assert.AreNotEqual(0, pc.getCount(""));
            Assert.AreEqual(0, pc.getCount("test"));
            Assert.IsTrue(pc.getCount(u.type) >= 3);

            Assert.AreEqual(0L, pc.getCount(null, null));
            Assert.AreEqual(0L, pc.getCount(u.type, new Dictionary<string, object> { { "id", " " } }));
            Assert.AreEqual(1L, pc.getCount(u.type, new Dictionary<string, object> { { "id", u.id } }));
            Assert.IsTrue(pc.getCount(null, new Dictionary<string, object> { { "type", u.type } }) > 1);
        }

        [Test]
        public void testLinks()
        {
            Assert.IsNotNull(pc.link(u, t.id));
            Assert.IsNotNull(pc.link(u, u2.id));

            Assert.IsFalse(pc.isLinked(u, null));
            Assert.IsTrue(pc.isLinked(u, t));
            Assert.IsTrue(pc.isLinked(u, u2));

            Thread.Sleep(1000);

            Assert.AreEqual(1, pc.getLinkedObjects(u, "tag").Count);
            Assert.AreEqual(1, pc.getLinkedObjects(u, "sysprop").Count);

            Assert.AreEqual(0, pc.countLinks(u, null));
            Assert.AreEqual(1, pc.countLinks(u, "tag"));
            Assert.AreEqual(1, pc.countLinks(u, "sysprop"));

		    pc.unlinkAll(u);

            Assert.IsFalse(pc.isLinked(u, t));
            Assert.IsFalse(pc.isLinked(u, u2));
        }

        [Test]
        public void testUtils()
        {
            string id1 = pc.newId();
            string id2 = pc.newId();
            Assert.IsNotNull(id1);
            Assert.IsFalse(string.IsNullOrEmpty(id1));
            Assert.AreNotEqual(id1, id2);

            long ts = pc.getTimestamp();
            Assert.IsNotNull(ts);
            Assert.AreNotEqual(0, ts);

            string date1 = pc.formatDate("MM dd yyyy", "US");
            string date2 = DateTime.Now.ToString("MM dd yyyy");
            Assert.AreEqual(date1, date2);

            string ns1 = pc.noSpaces(" test  123		test ", "");
            Assert.AreEqual(ns1, "test123test");

            string st1 = pc.stripAndTrim(" %^&*( cool )		@!");
            Assert.AreEqual(st1, "cool");

            string md1 = pc.markdownToHtml("# hello **test**");
            Assert.AreEqual(md1, "<h1>hello <strong>test</strong></h1>\n");

            string ht1 = pc.approximately(15000);
            Assert.AreEqual(ht1, "15s");
        }

        [Test]
        public void testMisc()
        {
            Dictionary<string, string> types = pc.types();
            Assert.NotNull(types);
            Assert.IsFalse(types.Count == 0);
            Assert.IsTrue(types.ContainsKey(new ParaObject(null, "user").getPlural()));

            Assert.AreEqual("app:para", pc.me().id);
        }

        [Test]
        public void testValidationConstraints()
        {
            // Validations
            string kittenType = "kitten";
            Dictionary<string, object> constraints = pc.validationConstraints();
            Assert.IsFalse(constraints.Count == 0);
            Assert.IsTrue(constraints.ContainsKey("app"));
            Assert.IsTrue(constraints.ContainsKey("user"));

            Dictionary<string, Dictionary<string, Dictionary<string, object>>> constraint = 
                pc.validationConstraints("app");
            Assert.IsFalse(constraint.Count == 0);
            Assert.IsTrue(constraint.ContainsKey("app"));
            Assert.AreEqual(1, constraint.Count);

            pc.addValidationConstraint(kittenType, "paws", Constraint.required());
            constraint = pc.validationConstraints(kittenType);
            Assert.IsTrue(constraint[kittenType].ContainsKey("paws"));

            ParaObject ct = new ParaObject("felix");
            ct.type = kittenType;
            ParaObject ct2 = null;
            try {
                // validation fails
                ct2 = pc.create (ct);
            } catch { }

            Assert.IsNull(ct2);
            ct["paws"] = "4";
            Assert.IsNotNull(pc.create(ct));

            pc.removeValidationConstraint(kittenType, "paws", "required");
            constraint = pc.validationConstraints(kittenType);
            Assert.IsFalse(constraint.ContainsKey(kittenType));

            // votes
            Assert.IsTrue(pc.voteUp(ct, u.id));
            Assert.IsFalse(pc.voteUp(ct, u.id));
            Assert.IsTrue(pc.voteDown(ct, u.id));
            Assert.IsTrue(pc.voteDown(ct, u.id));
            Assert.IsFalse(pc.voteDown(ct, u.id));
            pc.delete(ct);
            pc.delete(new ParaObject("vote:" + u.id + ":" + ct.id, "vote"));

            Assert.IsNotEmpty(pc.getServerVersion());
            Assert.AreNotEqual("unknown", pc.getServerVersion());
        }

        [Test]
        public void testResourcePermissions()
        {
            // Permissions
            var permits = pc.resourcePermissions();
            Assert.NotNull(permits);

            Assert.IsTrue(pc.grantResourcePermission(null, dogsType, new string[]{}).Count == 0);
            Assert.IsTrue(pc.grantResourcePermission(" ", "", new string[]{}).Count == 0);

            pc.grantResourcePermission(u1.id, dogsType, new [] {"GET"});
            permits = pc.resourcePermissions(u1.id);
            Assert.IsTrue(permits.ContainsKey(u1.id));
            Assert.IsTrue(permits[u1.id].ContainsKey(dogsType));
            Assert.IsTrue(pc.isAllowedTo(u1.id, dogsType, "GET"));
            Assert.IsFalse(pc.isAllowedTo(u1.id, dogsType, "POST"));
            // anonymous permissions
            Assert.IsFalse(pc.isAllowedTo("*", "utils/timestamp", "GET"));
            Assert.IsNotNull(pc.grantResourcePermission("*", "utils/timestamp", new [] {"GET"}, true));
            Assert.IsTrue(pc2.getTimestamp() > 0);
            Assert.IsFalse(pc.isAllowedTo("*", "utils/timestamp", "DELETE"));

            permits = pc.resourcePermissions();
            Assert.IsTrue(permits.ContainsKey(u1.id));
            Assert.IsTrue(permits[u1.id].ContainsKey(dogsType));

            pc.revokeResourcePermission(u1.id, dogsType);
            permits = pc.resourcePermissions(u1.id);
            Assert.IsFalse(permits[u1.id].ContainsKey(dogsType));
            Assert.IsFalse(pc.isAllowedTo(u1.id, dogsType, "GET"));
            Assert.IsFalse(pc.isAllowedTo(u1.id, dogsType, "POST"));

            pc.grantResourcePermission(u2.id, "*", new [] {"POST", "PUT", "PATCH", "DELETE"});
            Assert.IsTrue(pc.isAllowedTo(u2.id, dogsType, "PUT"));
            Assert.IsTrue(pc.isAllowedTo(u2.id, dogsType, "PATCH"));

            pc.revokeAllResourcePermissions(u2.id);
            permits = pc.resourcePermissions();
            Assert.IsFalse(pc.isAllowedTo(u2.id, dogsType, "PUT"));
            Assert.IsFalse(permits.ContainsKey(u2.id));
            //Assert.IsTrue(permits[u2.id].Count == 0);

            pc.grantResourcePermission(u1.id, dogsType, new [] {"POST", "PUT", "PATCH", "DELETE"});
            pc.grantResourcePermission("*", "*", new [] {"GET"});
            pc.grantResourcePermission("*", catsType, new [] {"POST", "PUT", "PATCH", "DELETE"});
            // user-specific permissions are in effect
            Assert.IsTrue(pc.isAllowedTo(u1.id, dogsType, "PUT"));
            Assert.IsFalse(pc.isAllowedTo(u1.id, dogsType, "GET"));
            Assert.IsTrue(pc.isAllowedTo(u1.id, catsType, "PUT"));
            Assert.IsTrue(pc.isAllowedTo(u1.id, catsType, "GET"));

            pc.revokeAllResourcePermissions(u1.id);
            // user-specific permissions not found so check wildcard
            Assert.IsFalse(pc.isAllowedTo(u1.id, dogsType, "PUT"));
            Assert.IsTrue(pc.isAllowedTo(u1.id, dogsType, "GET"));
            Assert.IsTrue(pc.isAllowedTo(u1.id, catsType, "PUT"));
            Assert.IsTrue(pc.isAllowedTo(u1.id, catsType, "GET"));

            pc.revokeResourcePermission("*", catsType);
            // resource-specific permissions not found so check wildcard
            Assert.IsFalse(pc.isAllowedTo(u1.id, dogsType, "PUT"));
            Assert.IsFalse(pc.isAllowedTo(u1.id, catsType, "PUT"));
            Assert.IsTrue(pc.isAllowedTo(u1.id, dogsType, "GET"));
            Assert.IsTrue(pc.isAllowedTo(u1.id, catsType, "GET"));
            Assert.IsTrue(pc.isAllowedTo(u2.id, dogsType, "GET"));
            Assert.IsTrue(pc.isAllowedTo(u2.id, catsType, "GET"));

            pc.revokeAllResourcePermissions("*");
            pc.revokeAllResourcePermissions(u1.id);
        }

        [Test]
        public void testAppSettings()
        {
            Dictionary<string, object> settings = pc.appSettings();
            Assert.NotNull(settings);
            Assert.IsTrue(settings.Count == 0);

            pc.addAppSetting("", null);
            pc.addAppSetting(" ", " ");
            pc.addAppSetting(null, " ");
            pc.addAppSetting("prop1", 1);
            pc.addAppSetting("prop2", true);
            pc.addAppSetting("prop3", "string");

            var x = pc.appSettings("prop1")["value"];
            Assert.IsTrue(pc.appSettings().Count == 3);
            Assert.IsTrue(pc.appSettings().Count == pc.appSettings(null).Count);
            Assert.IsTrue(pc.appSettings("prop1")["value"].Equals(1L));
            Assert.IsTrue(pc.appSettings("prop2")["value"].Equals(true));
            Assert.IsTrue(pc.appSettings("prop3")["value"].Equals("string"));

            pc.removeAppSetting("prop3");
            pc.removeAppSetting(" ");
            pc.removeAppSetting(null);
            Assert.IsTrue(pc.appSettings("prop3").Count == 0);
            Assert.IsTrue(pc.appSettings().Count == 2);
            pc.setAppSettings(new Dictionary<string, object>(0));
        }

//        [Test]
//        public void testAccessTokens()
//        {
//            Assert.IsNull(pc.getAccessToken());
//            Assert.IsNull(pc.signIn("facebook", "test_token"));
//            pc.signOut();
//            Assert.IsFalse(pc.revokeAllTokens());
//        }
    }
}
