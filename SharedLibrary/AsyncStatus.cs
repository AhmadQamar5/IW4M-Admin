﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibrary
{
    public sealed class AsyncStatus
    {
        CancellationToken Token;
        DateTime StartTime;
        int TimesRun;
        public double RunAverage { get; private set; }
        public object Dependant { get; private set; }
        public Task RequestedTask { get; private set; }

        public AsyncStatus(object dependant)
        {
            Token = new CancellationToken();
            StartTime = DateTime.Now;
            Dependant = dependant;
            // technically 0 but it's faster than checking for division by 0
            TimesRun = 1;
        }

        public CancellationToken GetToken()
        {
            return Token;
        }

        public double ElapsedMillisecondsTime()
        {
            return (DateTime.Now - StartTime).TotalMilliseconds;
        }

        public void Update(Task T)
        {
            RequestedTask = T;
            RequestedTask.Start();

            if (TimesRun > 100)
                TimesRun = 1;

            RunAverage = RunAverage + ((DateTime.Now - StartTime).TotalMilliseconds - RunAverage) / TimesRun;
            StartTime = DateTime.Now;
        }
    }
}
