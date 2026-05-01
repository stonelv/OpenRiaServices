using System;
using System.Collections.Generic;
using System.Linq;
using Cities;
using Microsoft.Silverlight.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenRiaServices.Silverlight.Testing;
using DescriptionAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute;

namespace OpenRiaServices.Client.Test
{
    [TestClass]
    public class CitiesDomainServiceTests : UnitTestBase
    {
        protected void After(Func<bool> condition)
        {
            EnqueueConditional(delegate() { return condition(); });
        }
        protected void Then(Action a)
        {
            EnqueueCallback(delegate() {a();});
        }

#if !ASPNETCORE
        [TestMethod]
        [Asynchronous]
        [Description("Verifies that a custom host is used to host CityDomainService")]
        [TestCategory("WCF")]
        public void Cities_VerifyCustomHost()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);    // Abs URI so runs on desktop too

            InvokeOperation<bool> invokeOp = dp.UsesCustomHost(TestHelperMethods.DefaultOperationAction, null);

            this.EnqueueCompletion(() => invokeOp);

            EnqueueCallback(() =>
            {
                if (invokeOp.Error != null)
                    Assert.Fail("InvokeOperation.Error: " + invokeOp.Error.Message);
                Assert.IsTrue(invokeOp.Value, "CityDomainService isn't using a custom host.");
            });

            EnqueueTestComplete();
        }
#endif

        /// <summary>
        /// Verify that Enum Entity properties are handled properly by testing
        /// both query and update scenarios
        /// </summary>
        [TestMethod]
        [Asynchronous]
        public void Cities_LoadStates_TestEnums()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);

            SubmitOperation so = null;
            LoadOperation lo = dp.Load(dp.GetStatesQuery().Where(s => s.TimeZone == Cities.TimeZone.Pacific), false);

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);

                // verify the TimeZones were serialized to the client properly
                State state = dp.States.Single(p => p.Name == "WA");
                Assert.AreEqual(Cities.TimeZone.Pacific, state.TimeZone);

                Assert.IsFalse(dp.States.Any(p => p.Name == "OH"));

                // Now test update
                state.TimeZone = state.TimeZone = Cities.TimeZone.Central;
                Assert.AreEqual(EntityState.Modified, state.EntityState);

                EntityChangeSet cs = dp.EntityContainer.GetChanges();
                Assert.Contains(state, cs.ModifiedEntities);

                so = dp.SubmitChanges(TestHelperMethods.DefaultOperationAction, null);
            });
            this.EnqueueCompletion(() => so);
            EnqueueCallback(() =>
            {
                TestHelperMethods.AssertOperationSuccess(so);
            });

            EnqueueTestComplete();
        }

        /// <summary>
        /// Verify that Enum Entity properties are handled properly by testing
        /// both query and update scenarios
        /// </summary>
        [TestMethod]
        [Description("Loads states using a query method that takes a generated enum type")]
        [Asynchronous]
        public void Cities_LoadStates_TestEnums_Generated()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);

            SubmitOperation so = null;
            LoadOperation lo = dp.Load(dp.GetStatesInShippingZoneQuery(ShippingZone.Eastern), false);

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);

                // verify the TimeZones were serialized to the client properly
                State state = dp.States.Single(p => p.Name == "OH");
                Assert.AreEqual(Cities.ShippingZone.Eastern, state.ShippingZone);

                // Now test update
                state.ShippingZone = Cities.ShippingZone.Central;
                Assert.AreEqual(EntityState.Modified, state.EntityState);

                EntityChangeSet cs = dp.EntityContainer.GetChanges();
                Assert.Contains(state, cs.ModifiedEntities);

                so = dp.SubmitChanges(TestHelperMethods.DefaultOperationAction, null);
            });
            this.EnqueueCompletion(() => so);
            EnqueueCallback(() =>
            {
                Assert.IsNull(so.Error);
            });

            EnqueueTestComplete();
        }


        [TestMethod]
        [Asynchronous]
        [Description("Simple load of all Cities from CityDomainContext")]
        public void Cities_TestLoad()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);    // Abs URI so runs on desktop too

            LoadOperation lo = dp.Load(dp.GetCitiesQuery(), false);

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() => 
                {
                    if (lo.Error != null)
                        Assert.Fail("LoadOperation.Error: " + lo.Error.Message);
                    IEnumerable<City> expected = new CityData().Cities;
                    AssertSame(expected, dp.Cities);
                });

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        [Description("Simple load of all Cities from CityDomainContext")]
        public void Cities_TestLoad_Demo()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);    // Abs URI so runs on desktop too

            LoadOperation lo = dp.Load(dp.GetCitiesQuery(), false);

            After(() => lo.IsComplete);

            Then(() => {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);
            });

            Then(() => AssertSame(new CityData().Cities, dp.Cities));

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        [Description("Load Cities but pass the state name as a parameter to the server")]
        public void Cities_Cities_In_State_Parameterized_Query()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);    // Abs URI so runs on desktop too

            LoadOperation lo = dp.Load(dp.GetCitiesInStateQuery("WA"), false);

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);
                IEnumerable<City> expected = new CityData().Cities.Where(c => c.StateName.Equals("WA"));
                AssertSame(expected, dp.Cities);

                // Validate a [Editable(false)] property deserialized properly
                foreach (City c in dp.Cities)
                    Assert.AreEqual(c.CountyName, c.CalculatedCounty);
           });

            EnqueueTestComplete();
        }

        /// <summary>
        /// Ensure that if an empty string is passed for a string parameter, it flows
        /// all the way to the server DomainOperationEntry as an empty string
        /// </summary>
        [TestMethod]
        [Asynchronous]
        public void TestEmptyStringParameter()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);    

            LoadOperation lo = dp.Load(dp.GetCitiesInStateQuery(string.Empty), false);

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);
                Assert.AreEqual(0, dp.Cities.Count);
            });

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        [Description("Loads Cities using a query expression composed locally that runs on the server")]
        public void Cities_Cities_In_County_Serialized_Query()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);    // Abs URI so runs on desktop too

            // Pass the query to the server to select only cities in King county
            var cityQuery = dp.GetCitiesQuery().Where(c => c.CountyName == "King");
            LoadOperation lo = dp.Load(cityQuery, false);

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);
                IEnumerable<City> expected = new CityData().Cities.Where(c => c.CountyName == "King");
                AssertSame(expected, dp.Cities);
            });

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        public void Cities_ShouldSupportLongQueries()
        {
            LoadOperation<Zip> lo = null;
            const int zipToFind = 98053;
            const int QUERY_ITERATIONS = 50;

            EnqueueCallback(() =>
            {
                CityDomainContext dp = new CityDomainContext(TestURIs.Cities);    // Abs URI so runs on desktop too

                // Generate a really long query
                // The load will result in a query where just the query part has length > 3000
                var query = dp.GetZipsQuery();

                // Create a query with QUERY_ITERATIONS where statements checking a range of QUERY_ITERATIONS each
                // this should in the end if simplified result in Code = zipToFind (zipToFind - 1 < Code  <= zipToFind)
                for (int i = 0; i < QUERY_ITERATIONS; ++i)
                {
                    int min = zipToFind + i - QUERY_ITERATIONS;
                    int max = zipToFind + i;
                    query = query.Where(c => min < c.Code && c.Code <= max);
                }

                lo = dp.Load(query, false);
            });

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);
                var expected = new CityData().Zips.Single(z => z.Code == zipToFind);
                Assert.HasCount(1, lo.Entities, "Wrong number of entities returned");
                var returned = lo.Entities.Single();

                Assert.AreEqual(expected.Code, returned.Code);
                Assert.AreEqual(expected.FourDigit, returned.FourDigit); 
                Assert.AreEqual(expected.CityName, returned.CityName);
                Assert.AreEqual(expected.CountyName, returned.CountyName);
                Assert.AreEqual(expected.StateName, returned.StateName);
            });
            EnqueueTestComplete();
        }

        private void AssertSame(IEnumerable<City> expected, IEnumerable<City> actual)
        {
            Assert.AreEqual(expected.Count(), actual.Count(), "Local CityData has different number of results than query result");
            foreach (City c1 in expected)
            {
                City bestActual = null;
                foreach (City c2 in actual)
                {
                    if (c2.StateName.Equals(c1.StateName) && 
                        c2.Name.Equals(c1.Name) &&
                        c2.CountyName.Equals(c1.CountyName))
                    {
                        bestActual = c2;
                        break;
                    }
                }
                Assert.IsNotNull(bestActual, "Could not find city " + c1.Name + " in actual results");
            }
        }

        private static void EnsureContainsAll(IEnumerable<string> actual, IEnumerable<string> expected)
        {
            foreach (string s in expected)
                Assert.IsTrue(actual.Contains(s), "Expected to find " + s);
            Assert.AreEqual(expected.Count(), actual.Count());
        }

        #region GetCitiesWithPaging End-to-End Tests

        [TestMethod]
        [Asynchronous]
        [Description("Test GetCitiesWithPagingQuery basic query with TotalCount")]
        public void Cities_GetCitiesWithPaging_Basic()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);

            var query = dp.GetCitiesWithPagingQuery();
            LoadOperation<City> lo = dp.Load(query, false);

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);

                var expectedCities = new CityData().Cities;
                Assert.AreEqual(expectedCities.Count, lo.TotalEntityCount, "TotalEntityCount should match total number of cities");
                Assert.AreEqual(expectedCities.Count, lo.Entities.Count(), "Should return all cities when no paging is applied");
            });

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        [Description("Test GetCitiesWithPaging with Where filter")]
        public void Cities_GetCitiesWithPaging_WhereFilter()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);

            var query = dp.GetCitiesWithPagingQuery().Where(c => c.StateName == "WA");
            LoadOperation<City> lo = dp.Load(query, false);

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);

                var allCities = new CityData().Cities;
                var waCities = allCities.Where(c => c.StateName == "WA").ToList();

                Assert.AreEqual(allCities.Count, lo.TotalEntityCount, "TotalEntityCount should be the full count (from out parameter)");
                Assert.AreEqual(waCities.Count, lo.Entities.Count(), "Should return only WA cities");
                Assert.IsTrue(lo.Entities.All(c => c.StateName == "WA"), "All returned cities should be in WA state");
            });

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        [Description("Test GetCitiesWithPaging with OrderBy - ordering stability")]
        public void Cities_GetCitiesWithPaging_OrderBy_Stability()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);
            CityDomainContext dp2 = new CityDomainContext(TestURIs.Cities);

            var query = dp.GetCitiesWithPagingQuery().OrderBy(c => c.Name).ThenBy(c => c.StateName);
            var query2 = dp2.GetCitiesWithPagingQuery().OrderBy(c => c.Name).ThenBy(c => c.StateName);

            LoadOperation<City> lo = dp.Load(query, false);
            LoadOperation<City> lo2 = dp2.Load(query2, false);

            this.EnqueueCompletion(() => lo);
            this.EnqueueCompletion(() => lo2);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);
                if (lo2.Error != null)
                    Assert.Fail("LoadOperation2.Error: " + lo2.Error.Message);

                var names1 = lo.Entities.Select(c => c.Name + "|" + c.StateName).ToList();
                var names2 = lo2.Entities.Select(c => c.Name + "|" + c.StateName).ToList();

                CollectionAssert.AreEqual(names1, names2, "Ordering should be stable across multiple queries");

                var expected = new CityData().Cities
                    .OrderBy(c => c.Name)
                    .ThenBy(c => c.StateName)
                    .Select(c => c.Name + "|" + c.StateName)
                    .ToList();

                CollectionAssert.AreEqual(expected, names1, "Ordering should match expected sorted order");
            });

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        [Description("Test GetCitiesWithPaging with OrderByDescending")]
        public void Cities_GetCitiesWithPaging_OrderByDescending()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);

            var query = dp.GetCitiesWithPagingQuery().OrderByDescending(c => c.Name);
            LoadOperation<City> lo = dp.Load(query, false);

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);

                var names = lo.Entities.Select(c => c.Name).ToList();
                var expectedDescending = new CityData().Cities
                    .OrderByDescending(c => c.Name)
                    .Select(c => c.Name)
                    .ToList();

                CollectionAssert.AreEqual(expectedDescending, names, "Should be ordered in descending order");
            });

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        [Description("Test GetCitiesWithPaging with Skip and Take for paging")]
        public void Cities_GetCitiesWithPaging_SkipTake()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);
            CityDomainContext dp2 = new CityDomainContext(TestURIs.Cities);

            int skip = 2;
            int take = 3;

            var allQuery = dp.GetCitiesWithPagingQuery().OrderBy(c => c.Name);
            var pagedQuery = dp2.GetCitiesWithPagingQuery().OrderBy(c => c.Name).Skip(skip).Take(take);

            LoadOperation<City> loAll = dp.Load(allQuery, false);
            LoadOperation<City> loPaged = dp2.Load(pagedQuery, false);

            this.EnqueueCompletion(() => loAll);
            this.EnqueueCompletion(() => loPaged);

            EnqueueCallback(() =>
            {
                if (loAll.Error != null)
                    Assert.Fail("loAll.Error: " + loAll.Error.Message);
                if (loPaged.Error != null)
                    Assert.Fail("loPaged.Error: " + loPaged.Error.Message);

                var allCities = new CityData().Cities;
                var expectedPaged = allCities
                    .OrderBy(c => c.Name)
                    .Skip(skip)
                    .Take(take)
                    .Select(c => c.Name)
                    .ToList();

                Assert.AreEqual(allCities.Count, loAll.TotalEntityCount, "TotalEntityCount should be full count for all query");
                Assert.AreEqual(allCities.Count, loPaged.TotalEntityCount, "TotalEntityCount should be full count even for paged query");

                Assert.AreEqual(take, loPaged.Entities.Count(), "Should return exactly 'take' items");

                var pagedNames = loPaged.Entities.Select(c => c.Name).ToList();
                CollectionAssert.AreEqual(expectedPaged, pagedNames, "Paged results should be correct");
            });

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        [Description("Test GetCitiesWithPaging paging boundary conditions")]
        public void Cities_GetCitiesWithPaging_PagingBoundaries()
        {
            var allCities = new CityData().Cities;
            int totalCities = allCities.Count;

            CityDomainContext dp1 = new CityDomainContext(TestURIs.Cities);
            CityDomainContext dp2 = new CityDomainContext(TestURIs.Cities);
            CityDomainContext dp3 = new CityDomainContext(TestURIs.Cities);

            var querySkipAll = dp1.GetCitiesWithPagingQuery().Skip(totalCities);
            var querySkipBeyond = dp2.GetCitiesWithPagingQuery().Skip(totalCities + 10);
            var queryTakeZero = dp3.GetCitiesWithPagingQuery().Take(0);

            LoadOperation<City> lo1 = dp1.Load(querySkipAll, false);
            LoadOperation<City> lo2 = dp2.Load(querySkipBeyond, false);
            LoadOperation<City> lo3 = dp3.Load(queryTakeZero, false);

            this.EnqueueCompletion(() => lo1);
            this.EnqueueCompletion(() => lo2);
            this.EnqueueCompletion(() => lo3);

            EnqueueCallback(() =>
            {
                if (lo1.Error != null)
                    Assert.Fail("lo1.Error: " + lo1.Error.Message);
                if (lo2.Error != null)
                    Assert.Fail("lo2.Error: " + lo2.Error.Message);
                if (lo3.Error != null)
                    Assert.Fail("lo3.Error: " + lo3.Error.Message);

                Assert.AreEqual(totalCities, lo1.TotalEntityCount, "TotalEntityCount should be correct when Skip equals total");
                Assert.AreEqual(0, lo1.Entities.Count(), "Should return 0 results when Skip equals total count");

                Assert.AreEqual(totalCities, lo2.TotalEntityCount, "TotalEntityCount should be correct when Skip exceeds total");
                Assert.AreEqual(0, lo2.Entities.Count(), "Should return 0 results when Skip exceeds total count");

                Assert.AreEqual(totalCities, lo3.TotalEntityCount, "TotalEntityCount should be correct with Take(0)");
                Assert.AreEqual(0, lo3.Entities.Count(), "Should return 0 results with Take(0)");
            });

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        [Description("Test GetCitiesWithPaging with combined Where, OrderBy, Skip, Take")]
        public void Cities_GetCitiesWithPaging_CombinedQuery()
        {
            CityDomainContext dp = new CityDomainContext(TestURIs.Cities);

            var allCities = new CityData().Cities;
            var waCities = allCities.Where(c => c.StateName == "WA").OrderBy(c => c.Name).ToList();
            int skip = 1;
            int take = 3;

            var query = dp.GetCitiesWithPagingQuery()
                .Where(c => c.StateName == "WA")
                .OrderBy(c => c.Name)
                .Skip(skip)
                .Take(take);

            LoadOperation<City> lo = dp.Load(query, false);

            this.EnqueueCompletion(() => lo);

            EnqueueCallback(() =>
            {
                if (lo.Error != null)
                    Assert.Fail("LoadOperation.Error: " + lo.Error.Message);

                var expectedPaged = waCities.Skip(skip).Take(take).Select(c => c.Name).ToList();
                var resultNames = lo.Entities.Select(c => c.Name).ToList();

                Assert.AreEqual(allCities.Count, lo.TotalEntityCount, "TotalEntityCount should be from out parameter (full count)");
                Assert.AreEqual(expectedPaged.Count, lo.Entities.Count(), "Should return correct number of paged results");
                CollectionAssert.AreEqual(expectedPaged, resultNames, "Results should be filtered, sorted, and paged correctly");
                Assert.IsTrue(lo.Entities.All(c => c.StateName == "WA"), "All results should be filtered to WA state");
            });

            EnqueueTestComplete();
        }

        [TestMethod]
        [Asynchronous]
        [Description("Test GetCitiesWithPaging TotalCount correctness across multiple pages")]
        public void Cities_GetCitiesWithPaging_TotalCountConsistency()
        {
            CityDomainContext dp1 = new CityDomainContext(TestURIs.Cities);
            CityDomainContext dp2 = new CityDomainContext(TestURIs.Cities);
            CityDomainContext dp3 = new CityDomainContext(TestURIs.Cities);

            var query1 = dp1.GetCitiesWithPagingQuery();
            var query2 = dp2.GetCitiesWithPagingQuery().Skip(2).Take(3);
            var query3 = dp3.GetCitiesWithPagingQuery().Where(c => c.StateName == "WA").Skip(1).Take(2);

            LoadOperation<City> lo1 = dp1.Load(query1, false);
            LoadOperation<City> lo2 = dp2.Load(query2, false);
            LoadOperation<City> lo3 = dp3.Load(query3, false);

            this.EnqueueCompletion(() => lo1);
            this.EnqueueCompletion(() => lo2);
            this.EnqueueCompletion(() => lo3);

            EnqueueCallback(() =>
            {
                if (lo1.Error != null)
                    Assert.Fail("lo1.Error: " + lo1.Error.Message);
                if (lo2.Error != null)
                    Assert.Fail("lo2.Error: " + lo2.Error.Message);
                if (lo3.Error != null)
                    Assert.Fail("lo3.Error: " + lo3.Error.Message);

                var allCities = new CityData().Cities;
                int expectedTotalCount = allCities.Count;

                Assert.AreEqual(expectedTotalCount, lo1.TotalEntityCount, "TotalCount should match for basic query");
                Assert.AreEqual(expectedTotalCount, lo2.TotalEntityCount, "TotalCount should be consistent for paged query");
                Assert.AreEqual(expectedTotalCount, lo3.TotalEntityCount, "TotalCount should be consistent for filtered and paged query");
            });

            EnqueueTestComplete();
        }

        #endregion
    }
}
