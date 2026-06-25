using System;
using NUnit.Framework;

namespace NLog.Targets.NetworkJSON.Tests
{
    public class JsonConverterTest
    {
        [TestFixture(Category = "JsonConverter")]
        public class GetJsonMethod
        {
            [Test]
            public void ShouldCreateJsonCorrectly()
            {
                var timestamp = DateTime.Now;
                var logEvent = new LogEventInfo
                                   {
                                       Message = "Test Log Message", 
                                       Level = LogLevel.Info, 
                                       TimeStamp = timestamp,
                                       LoggerName = "JsonConverterTestLogger"
                                   };
                logEvent.Properties.Add("customproperty1", "customvalue1");
                logEvent.Properties.Add("customproperty2", "customvalue2");
                logEvent.Properties.Add("custompropertyint", 199);
                logEvent.Properties.Add("custompropertyarray", new[]{1,2,3});

                var jsonObject = new JsonConverter().GetLogEventJson(logEvent);

                Assert.That(jsonObject, Is.Not.Null);
                // Always present properties
                Assert.That(jsonObject.Value<string>("message"), Is.EqualTo("Test Log Message"));
                Assert.That(jsonObject.Value<DateTime>("clientTimestamp"), Is.EqualTo(timestamp));
                Assert.That(jsonObject.Value<string>("logLevel"), Is.EqualTo(LogLevel.Info.ToString()));
                Assert.That(jsonObject.Value<int>("logSequenceId"), Is.GreaterThan(0));
                
                // Custom properties
                Assert.That(jsonObject.Value<string>("customproperty1"), Is.EqualTo("customvalue1"));
                Assert.That(jsonObject.Value<string>("customproperty2"), Is.EqualTo("customvalue2"));
                Assert.That(jsonObject.Value<int>("custompropertyint"), Is.EqualTo(199));
                Assert.That(jsonObject["custompropertyarray"].ToObject<int[]>(), Is.EqualTo(new[] { 1, 2, 3 }));

                // Make sure we have our 8 base properties (4 required and 4 custom).
                Assert.That(jsonObject.Count, Is.EqualTo(8));
            }

            [Test]
            public void ShouldHandleExceptionCorrectly()
            {
                var timestamp = DateTime.Now;
                var logEvent = new LogEventInfo
                {
                    Message = "Test Message",
                    Exception = new DivideByZeroException("div by 0"),
                    Level = LogLevel.Error,
                    TimeStamp = timestamp,
                    LoggerName = "JsonConverterTestLogger"
                };

                var jsonObject = new JsonConverter().GetLogEventJson(logEvent);

                Assert.That(jsonObject, Is.Not.Null);
                Assert.That(jsonObject.Value<string>("message"), Is.EqualTo("Test Message"));
                Assert.That(jsonObject.Value<DateTime>("clientTimestamp"), Is.EqualTo(timestamp));
                Assert.That(jsonObject.Value<string>("logLevel"), Is.EqualTo(LogLevel.Error.ToString()));
                Assert.That(jsonObject.Value<int>("logSequenceId"), Is.GreaterThan(0));

                Assert.That(jsonObject.Value<string>("ExceptionSource"), Is.Null);
                Assert.That(jsonObject.Value<string>("ExceptionMessage"), Is.EqualTo("div by 0"));
                Assert.That(jsonObject.Value<string>("StackTrace"), Is.Null);

                // Base properties plus 3 new properties related to exceptions
                Assert.That(jsonObject.Count, Is.EqualTo(7));
            }

            [Test]
            public void ShouldHandleNestedExceptionCorrectly()
            {
                var timestamp = DateTime.Now;
                var logEvent = new LogEventInfo
                {
                    Message = "Test Message",
                    Exception = new Exception("Outer Exception Detail", new Exception("Inner Exception Detail")),
                    Level = LogLevel.Error,
                    TimeStamp = timestamp,
                    LoggerName = "JsonConverterTestLogger"
                };

                var jsonObject = new JsonConverter().GetLogEventJson(logEvent);

                Assert.That(jsonObject, Is.Not.Null);
                Assert.That(jsonObject.Value<string>("message"), Is.EqualTo("Test Message"));
                Assert.That(jsonObject.Value<DateTime>("clientTimestamp"), Is.EqualTo(timestamp));
                Assert.That(jsonObject.Value<string>("logLevel"), Is.EqualTo(LogLevel.Error.ToString()));
                Assert.That(jsonObject.Value<int>("logSequenceId"), Is.GreaterThan(0));

                Assert.That(jsonObject.Value<string>("ExceptionSource"), Is.Null);
                Assert.That(jsonObject.Value<string>("ExceptionMessage"), Is.EqualTo("Outer Exception Detail - Inner Exception Detail"));
                Assert.That(jsonObject.Value<string>("StackTrace"), Is.Null);

                // Base properties plus 3 new properties related to exceptions
                Assert.That(jsonObject.Count, Is.EqualTo(7));
            }

            [Test]
            public void ShouldHandle10NestedExceptionCorrectly()
            {
                // It should ignore this 11th nested exception as 10 is the max it will handle.
                var nestedException = new Exception("Inner Exception Detail - 10");
                for (var i = 9; i > 0; i--)
                {
                    var nextException = new Exception("Inner Exception Detail - " + i.ToString(), nestedException);
                    nestedException = nextException;
                }
                var outerException = new Exception("Outer Exception Detail", nestedException);

                var timestamp = DateTime.Now;
                var logEvent = new LogEventInfo
                {
                    Message = "Test Message",
                    Exception = outerException,
                    Level = LogLevel.Error,
                    TimeStamp = timestamp,
                    LoggerName = "JsonConverterTestLogger"
                };

                var jsonObject = new JsonConverter().GetLogEventJson(logEvent);

                Assert.That(jsonObject, Is.Not.Null);
                Assert.That(jsonObject.Value<string>("message"), Is.EqualTo("Test Message"));
                Assert.That(jsonObject.Value<DateTime>("clientTimestamp"), Is.EqualTo(timestamp));
                Assert.That(jsonObject.Value<string>("logLevel"), Is.EqualTo(LogLevel.Error.ToString()));
                Assert.That(jsonObject.Value<int>("logSequenceId"), Is.GreaterThan(0));

                Assert.That(jsonObject.Value<string>("ExceptionSource"), Is.Null);
                const string expectedExceptionDetail =
                    "Outer Exception Detail - Inner Exception Detail - 1 - Inner Exception Detail - 2 - Inner Exception Detail - 3 - Inner Exception Detail - 4 - Inner Exception Detail - 5 - Inner Exception Detail - 6 - Inner Exception Detail - 7 - Inner Exception Detail - 8 - Inner Exception Detail - 9 - Inner Exception Detail - 10";
                Assert.That(jsonObject.Value<string>("ExceptionMessage"), Is.EqualTo(expectedExceptionDetail));
                Assert.That(jsonObject.Value<string>("StackTrace"), Is.Null);

                // Base properties plus 3 new properties related to exceptions
                Assert.That(jsonObject.Count, Is.EqualTo(7));
            }

            [Test]
            public void ShouldHandleLongMessageCorrectly()
            {
                var timestamp = DateTime.Now;
                var logEvent = new LogEventInfo
                {
                    //The first 300 chars of lorem ipsum...
                    Message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Phasellus interdum est in est cursus vitae pellentesque felis lobortis. Donec a orci quis ante viverra eleifend ac et quam. Donec imperdiet libero ut justo tincidunt non tristique mauris gravida. Fusce sapien eros, tincidunt a placerat nullam.",
                    Level = LogLevel.Info,
                    TimeStamp = timestamp,
                    LoggerName = "JsonConverterTestLogger"
                };

                var jsonObject = new JsonConverter().GetLogEventJson(logEvent);

                Assert.That(jsonObject, Is.Not.Null);
                Assert.That(jsonObject.Value<string>("message").Length, Is.EqualTo(300));
                Assert.That(jsonObject.Value<DateTime>("clientTimestamp"), Is.EqualTo(timestamp));
                Assert.That(jsonObject.Value<string>("logLevel"), Is.EqualTo(LogLevel.Info.ToString()));
                Assert.That(jsonObject.Value<int>("logSequenceId"), Is.GreaterThan(0));
            }
        }
    }
}
