﻿using Devkoes.Restup.WebServer.Models.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Devkoes.Restup.WebServer.Models.Schemas
{
    public struct HttpResponse : IHttpResponse
    {
        public string Body { get; }
        public MediaType BodyType { get; }
        public int StatusCode { get; }

        public HttpResponse(string body, MediaType bodyType, int statusCode)
        {
            Body = body;
            BodyType = bodyType;
            StatusCode = statusCode;
        }
    }
}