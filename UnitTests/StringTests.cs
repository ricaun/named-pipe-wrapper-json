﻿using NamedPipeWrapper;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace UnitTests
{
    public class StringTests
    {
        private static readonly log4net.ILog Logger =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string PipeName = "string_test_pipe";

        private NamedPipeServer<string> _server;
        private NamedPipeClient<string> _client;

        private string _expectedData;
        private string _actualData;

        private DateTime _startTime;

        private readonly ManualResetEvent _barrier = new ManualResetEvent(false);

        private readonly IList<Exception> _exceptions = new List<Exception>();


        #region Setup and teardown

        [SetUp]
        public void SetUp()
        {
            Logger.Debug("Setting up test...");

            _barrier.Reset();
            _exceptions.Clear();

            _server = new NamedPipeServer<string>(PipeName);
            _client = new NamedPipeClient<string>(PipeName);

            _expectedData = null;
            _actualData = null;

            _server.ClientMessage += ServerOnClientMessage;

            _server.Error += OnError;
            _client.Error += OnError;

            _server.Start();
            _client.Start();

            // Give the client and server a few seconds to connect before sending data
            Thread.Sleep(TimeSpan.FromSeconds(1));

            Logger.Debug("Client and server started");
            Logger.Debug("---");

            _startTime = DateTime.Now;
        }

        private void OnError(Exception exception)
        {
            _exceptions.Add(exception);
            _barrier.Set();
        }

        [TearDown]
        public void TearDown()
        {
            Logger.Debug("---");
            Logger.Debug("Stopping client and server...");

            _server.ClientMessage -= ServerOnClientMessage;

            _server.Stop();
            _client.Stop();

            Logger.Debug("Client and server stopped");
            Logger.DebugFormat("Test took {0}", (DateTime.Now - _startTime));
            Logger.Debug("~~~~~~~~~~~~~~~~~~~~~~~~~~");
        }

        #endregion

        #region Events

        private void ServerOnClientMessage(NamedPipeConnection<string, string> connection, string message)
        {
            Logger.DebugFormat("Received {0} bytes from the client", message.ToString().Length);
            _actualData = message;
            _barrier.Set();
        }

        #endregion

        [Test]
        public void TestCircularReferences()
        {
            var random = new Random();
            for (int i = 0; i < 10; i++)
            {
                TestClass(random.Next().ToString());
            }
        }

        [Test]
        public void TestEvent_ConnectedDisconnected()
        {
            var message = "";
            var client = new NamedPipeClient<string>(PipeName);
            client.Connected += (connection) =>
            {
                message = $"Connected: {connection.Id}";
                Console.WriteLine(message);
            };
            client.Disconnected += (connection) =>
            {
                message = $"Disconnected: {connection.Id}";
                Console.WriteLine(message);
            };
            client.Start();
            client.WaitForConnection(TimeSpan.FromSeconds(2));
            Console.WriteLine("---");

            Assert.IsTrue(message.Contains("Connected"), "Event Connected trigger before WaitForConnection.");

            client.Stop();
            client.WaitForDisconnection(TimeSpan.FromSeconds(2));
            Console.WriteLine("---");

            Assert.IsTrue(message.Contains("Disconnected"), "Event Disconnected trigger before WaitForDisconnection.");
        }

        private void TestClass(string _expectedValue)
        {
            _expectedData = _expectedValue;

            _barrier.Reset();
            _client.PushMessage(_expectedData);
            _barrier.WaitOne(TimeSpan.FromSeconds(30));

            if (_exceptions.Any())
                throw new AggregateException(_exceptions);

            Assert.AreEqual(_expectedData, _actualData, string.Format("Data should be equal"));

            var _actualValue = _actualData;

            Assert.AreEqual(_expectedValue, _actualValue, string.Format("Data should be equal"));
            Assert.AreEqual(_expectedValue.ToString(), _actualValue.ToString(), string.Format("Data should be equal"));
        }
    }
}
