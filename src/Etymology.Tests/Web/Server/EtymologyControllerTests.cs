﻿namespace Etymology.Tests.Web.Server
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Etymology.Common;
    using Etymology.Data.Cache;
    using Etymology.Data.Models;
    using Etymology.Tests.Common;
    using Etymology.Tests.Data.Models;
    using Etymology.Web.Server.Controllers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Primitives;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EtymologyControllerTests
    {
        private static EtymologyController CreateController()
        {
            CharacterCache characterCache = new CharacterCache();
            characterCache.Initialize(EtymologyContextTests.CreateDatabase()).Wait();
            return new EtymologyController(
                EtymologyContextTests.CreateDatabase(),
                new NullLogger<EtymologyController>(),
                new MemoryCache(new OptionsWrapper<MemoryCacheOptions>(new MemoryCacheOptions())),
                characterCache)
                {
                    ControllerContext = new ControllerContext()
                        {
                            HttpContext = new DefaultHttpContext()
                        }
                };
        }

        [TestMethod]
        public async Task AnalyzeAsyncTest()
        {
            IEnumerable<Task> testTasks = ChineseTests.ChineseCharacters.Select(async item =>
            {
                EtymologyController controller = CreateController();
                controller.ControllerContext.HttpContext.Request.Headers.Add(nameof(Chinese), new StringValues(char.ConvertToUtf32(item.Text, 0).ToString("D")));
                ViewResult view;
                try
                {
                    view = await controller.AnalyzeAsync(item.Text) as ViewResult;
                }
                catch (Exception exception)
                {
                    Trace.WriteLine(exception);
                    throw;
                }
                Assert.IsNotNull(view);
                Assert.IsInstanceOfType(view.Model, typeof((string, TimeSpan, AnalyzeResult[])));
                var (chinese, duration, results) = ((string, TimeSpan, AnalyzeResult[]))view.Model;
                Assert.AreEqual(item.Text, chinese);
                Assert.IsTrue(duration > TimeSpan.Zero);
                Assert.IsNotNull(results);
            });
            await Task.WhenAll(testTasks);
        }

        [TestMethod]
        public async Task AnalyzeAsyncErrorTest()
        {
            IEnumerable<Task> testTasks = ChineseTests.OtherCharacters.Select(async item =>
            {
                EtymologyController controller = CreateController();
                try
                {
                    controller.ControllerContext.HttpContext.Request.Headers.Add(nameof(Chinese), new StringValues(char.ConvertToUtf32(item, 0).ToString("D")));
                }
                catch (Exception exception)
                {
                    Trace.WriteLine(exception);
                }

                Trace.WriteLine(item);
#if DEBUG
                BadRequestObjectResult badRequest = await controller.AnalyzeAsync(item) as BadRequestObjectResult;
#else
                BadRequestResult badRequest = await controller.AnalyzeAsync(item) as BadRequestResult;
#endif
                Assert.IsNotNull(badRequest);
#if DEBUG
                Assert.IsInstanceOfType(badRequest.Value, typeof(ArgumentException));
#endif
                Assert.AreEqual((int)HttpStatusCode.BadRequest, badRequest.StatusCode);
            });
            await Task.WhenAll(testTasks);
        }
    }
}
