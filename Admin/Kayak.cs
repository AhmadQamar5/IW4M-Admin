﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Kayak.Http;
using Kayak;


namespace IW4MAdmin
{
    class Scheduler : ISchedulerDelegate
    {
        public void OnException(IScheduler scheduler, Exception e)
        {
            Manager.GetInstance().Logger.WriteError("Web service has encountered an error - " + e.Message);
        }

        public void OnStop(IScheduler scheduler)
        {
            Manager.GetInstance().Logger.WriteDebug("Web service has been stopped...");
        }
    }

    class Request : IHttpRequestDelegate
    {
        public void OnRequest(HttpRequestHead request, IDataProducer requestBody, IHttpResponseDelegate response, string IP)
        {
            // Program.getManager().mainLog.Write("HTTP request received", SharedLibrary.Log.Level.Debug);
            NameValueCollection querySet = new NameValueCollection();

            if (request.QueryString != null)
                querySet = System.Web.HttpUtility.ParseQueryString(SharedLibrary.Utilities.removeNastyChars(request.QueryString));

            querySet.Set("IP", IP);
            SharedLibrary.HttpResponse requestedPage = WebService.getPage(request.Path, querySet, request.Headers);

            var headers = new HttpResponseHead()
            {
                Status = "200 OK",
                Headers = new Dictionary<string, string>()
                    {
                        { "Content-Type", requestedPage.contentType },
                        { "Content-Length", requestedPage.content.Length.ToString() },
                        { "Access-Control-Allow-Origin", "*" },
                    }
            };

            foreach (var key in requestedPage.additionalHeaders.Keys)
                headers.Headers.Add(key, requestedPage.additionalHeaders[key]);

            response.OnResponse(headers, new BufferedProducer(requestedPage.content));
        }
    }

    class BufferedProducer : IDataProducer
    {
        ArraySegment<byte> data;

        public BufferedProducer(string data) : this(data, Encoding.UTF8) { }
        public BufferedProducer(string data, Encoding encoding) : this(encoding.GetBytes(data)) { }
        public BufferedProducer(byte[] data) : this(new ArraySegment<byte>(data)) { }

        public BufferedProducer(ArraySegment<byte> data)
        {
            this.data = data;
        }

        public IDisposable Connect(IDataConsumer channel)
        {
            channel.OnData(data, null);
            channel.OnEnd();
            return null;
        }
    }

    class BufferedConsumer : IDataConsumer
    {
        List<ArraySegment<byte>> buffer = new List<ArraySegment<byte>>();
        Action<string> resultCallback;
        Action<Exception> errorCallback;

        public BufferedConsumer(Action<string> resultCallback, Action<Exception> errorCallback)
        {
            this.resultCallback = resultCallback;
            this.errorCallback = errorCallback;
        }

        public bool OnData(ArraySegment<byte> data, Action continuation)
        {
            buffer.Add(data);
            return false;
        }

        public void OnError(Exception error)
        {
            errorCallback(error);
        }

        public void OnEnd()
        {
            var str = buffer
                .Select(b => Encoding.UTF8.GetString(b.Array, b.Offset, b.Count))
                .Aggregate((result, next) => result + next);

            resultCallback(str);
        }
    }
}