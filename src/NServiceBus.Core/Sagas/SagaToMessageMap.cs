namespace NServiceBus.Sagas
{
    using System;

    class SagaToMessageMap
    {
        public Func<object, object> MessageProp;
        public string SagaPropName;
        public string MessageType;
    }
}