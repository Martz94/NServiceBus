namespace NServiceBus
{
    using System;
    using Pipeline;
    using Pipeline.Contexts;
    using Sagas;

    class InvokeHandlersBehavior : IBehavior<IncomingContext>
    {
        public void Invoke(IncomingContext context, Action next)
        {
            ActiveSagaInstance saga;

            //todo, stop using the Instance
            if (context.TryGet(out saga) && saga.NotFound && saga.Instance == context.MessageHandler.Instance)
            {
                next();
                return;
            }

            var messageHandler = context.MessageHandler;

            messageHandler.Invocation(messageHandler.Instance, context.IncomingLogicalMessage.Instance);
            next();
        }
    }
}