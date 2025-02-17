namespace NServiceBus.Unicast.Transport.Monitoring
{
    using System;
    using System.Diagnostics;
    using Logging;

    class ReceivePerformanceDiagnostics : IDisposable
    {
        const string CategoryName = "NServiceBus";
        static ILog Logger = LogManager.GetLogger<ReceivePerformanceDiagnostics>();
        readonly Address receiveAddress;
        bool enabled;
        PerformanceCounter failureRateCounter;
        PerformanceCounter successRateCounter;
        PerformanceCounter throughputCounter;

        public ReceivePerformanceDiagnostics(Address receiveAddress)
        {
            this.receiveAddress = receiveAddress;
        }


        public void Dispose()
        {
            //Injected at compile time
        }

        public void Initialize()
        {
            if (receiveAddress.Queue.Length > SByte.MaxValue)
            {
                throw new Exception(string.Format("The queue name ('{0}') is too long (longer then {1}) to register as a performance counter instance name. Please reduce the queue/endpoint name.", receiveAddress.Queue, (int)SByte.MaxValue));
            }

            if (!InstantiateCounter())
            {
                return;
            }

            enabled = true;
        }

        public void MessageProcessed()
        {
            if (!enabled)
            {
                return;
            }

            successRateCounter.Increment();
        }

        public void MessageFailed()
        {
            if (!enabled)
            {
                return;
            }

            failureRateCounter.Increment();
        }

        public void MessageDequeued()
        {
            if (!enabled)
            {
                return;
            }

            throughputCounter.Increment();
        }


        bool InstantiateCounter()
        {
            return SetupCounter("# of msgs successfully processed / sec", ref successRateCounter)
                   && SetupCounter("# of msgs pulled from the input queue /sec", ref throughputCounter)
                   && SetupCounter("# of msgs failures / sec", ref failureRateCounter);
        }

        bool SetupCounter(string counterName, ref PerformanceCounter counter)
        {
            try
            {
                counter = new PerformanceCounter(CategoryName, counterName, receiveAddress.Queue, false);
                //access the counter type to force a exception to be thrown if the counter doesn't exists
                // ReSharper disable once UnusedVariable
                var t = counter.CounterType; 
            }
            catch (Exception)
            {
                Logger.InfoFormat(
                    "NServiceBus performance counter for {1} is not set up correctly, no statistics will be emitted for the {0} queue. Execute the Install-NServiceBusPerformanceCounters cmdlet to create the counter.",
                    receiveAddress.Queue, counterName);
                return false;
            }
            Logger.DebugFormat("'{0}' counter initialized for '{1}'", counterName, receiveAddress);
            return true;
        }
    }
}
