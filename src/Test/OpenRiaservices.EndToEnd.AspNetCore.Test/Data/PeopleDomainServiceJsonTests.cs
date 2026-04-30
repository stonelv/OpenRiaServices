#if NET8_0_OR_GREATER

extern alias httpDomainClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using httpDomainClient::OpenRiaServices.Client.DomainClients;
using People;
using static People.PeopleDomainContext;

namespace OpenRiaServices.Client.Test
{
    [TestClass]
    public class PeopleDomainServiceJsonTests
    {
        private static DomainClient CreateJsonDomainClient()
        {
            var httpHandler = new HttpClientHandler();
            var factory = new JsonHttpDomainClientFactory(TestURIs.RootURI, httpHandler);
            return factory.CreateDomainClient(typeof(IPeopleDomainServiceContract), new Uri("People-PeopleDomainService", UriKind.Relative), false);
        }

        private static DomainClient CreateBinaryDomainClient()
        {
            var httpHandler = new HttpClientHandler();
            var factory = new BinaryHttpDomainClientFactory(TestURIs.RootURI, httpHandler);
            return factory.CreateDomainClient(typeof(IPeopleDomainServiceContract), new Uri("People-PeopleDomainService", UriKind.Relative), false);
        }

        [TestMethod]
        public async Task TestJsonQuery_DateOnlyProperty()
        {
            var domainClient = CreateJsonDomainClient();
            var domainContext = new PeopleDomainContext(domainClient);

            LoadResult<Person> result = await domainContext.LoadAsync(domainContext.GetPersonsQuery());
            Person person1 = result.Single(p => p.Name == "Erik");
            Person person2 = result.Single(p => p.Name == "Gustav");

            Assert.HasCount(2, result);
            Assert.AreEqual(new(1970, 1, 1), person1.FavouriteDay);
            Assert.AreEqual(new(1523, 6, 6), person2.FavouriteDay);
        }

        [TestMethod]
        public async Task TestJsonQuery_TimeOnlyProperty()
        {
            var domainClient = CreateJsonDomainClient();
            var domainContext = new PeopleDomainContext(domainClient);

            LoadResult<WorkdaySchedule> result = await domainContext.LoadAsync(domainContext.GetWorkdaySchedulesQuery());
            WorkdaySchedule schedule1 = result.Single(p => p.Id == 1);
            WorkdaySchedule schedule2 = result.Single(p => p.Id == 2);

            Assert.HasCount(3, result);
            Assert.AreEqual(new(8, 0), schedule1.StartTime);
            Assert.AreEqual(new(7, 45, 23, 555), schedule2.StartTime);
            Assert.AreEqual(new(17, 0), schedule1.EndTime);
        }

        [TestMethod]
        public async Task TestJsonQuery_WithDateOnlyParameter()
        {
            var domainClient = CreateJsonDomainClient();
            var domainContext = new PeopleDomainContext(domainClient);

            DateOnly favouriteDay = new(1970, 1, 1);
            LoadResult<Person> result = await domainContext.LoadAsync(domainContext.GetPersonsByFavouriteDayQuery(favouriteDay));

            Assert.HasCount(1, result);
            Assert.AreEqual(favouriteDay, result.Single().FavouriteDay);
        }

        [TestMethod]
        public async Task TestJsonQuery_WithNullableDateOnlyParameter()
        {
            var domainClient = CreateJsonDomainClient();
            var domainContext = new PeopleDomainContext(domainClient);

            DateOnly? weddingDay = new(1531, 9, 24);
            LoadResult<Person> result = await domainContext.LoadAsync(domainContext.GetPersonsByWeddingDayQuery(weddingDay));

            Assert.HasCount(1, result);
            Assert.AreEqual(weddingDay, result.Single().WeddingDay);
        }

        [TestMethod]
        public async Task TestJsonInvoke_DateOnlyReturnValue()
        {
            var domainClient = CreateJsonDomainClient();
            var domainContext = new PeopleDomainContext(domainClient);

            InvokeResult<DateOnly> result = await domainContext.GetFavouriteDayByNameAsync("Erik", CancellationToken.None);

            Assert.AreEqual(new(1970, 1, 1), result.Value);
        }

        [TestMethod]
        public async Task TestJsonInvoke_ComplexTypeWithDateOnly()
        {
            var domainClient = CreateJsonDomainClient();
            var domainContext = new PeopleDomainContext(domainClient);

            InvokeResult<Lifespan> result = await domainContext.GetPersonLifespanByNameAsync("Gustav", CancellationToken.None);

            Assert.AreEqual(new(1496, 5, 12), result.Value.Born);
            Assert.AreEqual(new(1560, 9, 29), result.Value.Dead);
        }

        [TestMethod]
        public async Task TestJsonInvoke_TimeOnlyReturnValue()
        {
            var domainClient = CreateJsonDomainClient();
            var domainContext = new PeopleDomainContext(domainClient);

            InvokeResult<TimeOnly> result = await domainContext.GetStartTimeByIdAsync(1, CancellationToken.None);

            Assert.AreEqual(new(8, 0), result.Value);
        }

        [TestMethod]
        public async Task TestJsonInvoke_ComplexTypeWithTimeOnly()
        {
            var domainClient = CreateJsonDomainClient();
            var domainContext = new PeopleDomainContext(domainClient);

            InvokeResult<LunchBreak> result = await domainContext.GetLunchBreakByIdAsync(1, CancellationToken.None);

            Assert.AreEqual(new(12, 0), result.Value.StartTime);
            Assert.AreEqual(new(13, 0), result.Value.EndTime);
        }

        [TestMethod]
        public async Task TestJsonVsBinary_QueryResultsMatch()
        {
            var jsonDomainClient = CreateJsonDomainClient();
            var jsonDomainContext = new PeopleDomainContext(jsonDomainClient);

            var binaryDomainClient = CreateBinaryDomainClient();
            var binaryDomainContext = new PeopleDomainContext(binaryDomainClient);

            var jsonResult = await jsonDomainContext.LoadAsync(jsonDomainContext.GetPersonsQuery());
            var binaryResult = await binaryDomainContext.LoadAsync(binaryDomainContext.GetPersonsQuery());

            Assert.AreEqual(binaryResult.Count, jsonResult.Count);

            foreach (var binaryPerson in binaryResult)
            {
                var jsonPerson = jsonResult.Single(p => p.Name == binaryPerson.Name);
                Assert.AreEqual(binaryPerson.FavouriteDay, jsonPerson.FavouriteDay);
                Assert.AreEqual(binaryPerson.WeddingDay, jsonPerson.WeddingDay);
            }
        }

        [TestMethod]
        public async Task TestJsonVsBinary_InvokeResultsMatch()
        {
            var jsonDomainClient = CreateJsonDomainClient();
            var jsonDomainContext = new PeopleDomainContext(jsonDomainClient);

            var binaryDomainClient = CreateBinaryDomainClient();
            var binaryDomainContext = new PeopleDomainContext(binaryDomainClient);

            var jsonResult = await jsonDomainContext.GetFavouriteDayByNameAsync("Erik", CancellationToken.None);
            var binaryResult = await binaryDomainContext.GetFavouriteDayByNameAsync("Erik", CancellationToken.None);

            Assert.AreEqual(binaryResult.Value, jsonResult.Value);
        }

        [TestMethod]
        public async Task TestJsonInvokeOperation()
        {
            var domainClient = CreateJsonDomainClient();
            var domainContext = new PeopleDomainContext(domainClient);
            
            InvokeOperation invoke = domainContext.GetStartTimeById(1);

            await invoke;

            Assert.IsNull(invoke.Error);
            Assert.AreEqual("GetStartTimeById", invoke.OperationName);
            Assert.HasCount(1, invoke.Parameters);
            Assert.AreEqual(1, invoke.Parameters["id"]);
            Assert.AreEqual(new TimeOnly(8, 0), invoke.Value);
        }
    }
}
#endif
