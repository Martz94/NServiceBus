namespace NServiceBus
{
    using System;
    using NServiceBus.Features;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    class AssociateMessageWithSagaBehavior:IBehavior<IncomingContext>
    {
        readonly ISagaMetaModel sagaModel;

        public AssociateMessageWithSagaBehavior(ISagaMetaModel sagaModel)
        {
            this.sagaModel = sagaModel;
        }

        public void Invoke(IncomingContext context, Action next)
        {
            if (context.IncomingLogicalMessage == null)
            {
                next();
                return;
            }

            var messageTypeId = context.IncomingLogicalMessage.MessageType.FullName;

            context.Set(sagaModel.FindByMessageType(messageTypeId));

            next();
        }

        public class Registration : RegisterStep
        {
            public Registration()
                : base("AssociateMessageWithSagaBehavior", typeof(AssociateMessageWithSagaBehavior), "Determines if the incoming message should hit a saga")
            {
                InsertBefore(WellKnownStep.InvokeSaga);
                InsertAfter(WellKnownStep.DeserializeMessages);
            }
        }
    }
}