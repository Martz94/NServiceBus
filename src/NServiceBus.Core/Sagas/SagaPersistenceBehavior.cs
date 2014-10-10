namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Saga;
    using NServiceBus.Sagas;
    using NServiceBus.Timeout;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;
    using NServiceBus.Unicast.Messages;

    class SagaPersistenceBehavior : IBehavior<IncomingContext>
    {
        public ISagaPersister SagaPersister { get; set; }

        public IDeferMessages MessageDeferrer { get; set; }

        public IMessageHandlerRegistry MessageHandlerRegistry { get; set; }

        public SagaConfigurationCache SagaConfigurationCache { get; set; }

        public void Invoke(IncomingContext context, Action next)
        {

            //todo - foreach
            var sagaMetadata = context.Get<IEnumerable<SagaMetadata>>().Single();

            var saga = context.MessageHandler.Instance as Saga.Saga;
            if (saga == null)
            {
                next();
                return;
            }

            currentContext = context;


            var sagaInstanceState = new ActiveSagaInstance(saga, sagaMetadata);

            //so that other behaviors can access the saga
            context.Set(sagaInstanceState);

            var loadedEntity = TryLoadSagaEntity(sagaMetadata, context.IncomingLogicalMessage);

            if (loadedEntity == null)
            {
                //todo - refactor to use the saga name instead
                var sagaType = (Type)sagaMetadata.Properties["saga-clr-type"];

                //if this message are not allowed to start the saga
                if (sagaMetadata.IsMessageAllowedToStartTheSaga(context.IncomingLogicalMessage.MessageType.FullName))
                {
                    sagaInstanceState.AttachNewEntity(CreateNewSagaEntity(sagaMetadata, context.IncomingLogicalMessage));
                }
                else
                {
                    sagaInstanceState.MarkAsNotFound();

                    InvokeSagaNotFoundHandlers(sagaType);
                }
            }
            else
            {
                sagaInstanceState.AttachExistingEntity(loadedEntity);
            }

            if (IsTimeoutMessage(context.IncomingLogicalMessage))
            {
                context.MessageHandler.Invocation = MessageHandlerRegistry.InvokeTimeout;
            }


            next();

            if (sagaInstanceState.NotFound)
            {
                return;
            }

            LogSaga(sagaInstanceState, context);

            if (saga.Completed)
            {
                if (!sagaInstanceState.IsNew)
                {
                    SagaPersister.Complete(saga.Entity);
                }

                if (saga.Entity.Id != Guid.Empty)
                {
                    NotifyTimeoutManagerThatSagaHasCompleted(saga);
                }

                logger.DebugFormat("Saga: '{0}' with Id: '{1}' has completed.", sagaInstanceState.Metadata.Name, saga.Entity.Id);
            }
            else
            {
                if (sagaInstanceState.IsNew)
                {
                    SagaPersister.Save(saga.Entity);
                }
                else
                {
                    SagaPersister.Update(saga.Entity);
                }
            }
        }

        bool IsAllowedToStartANewSaga(IncomingContext context, ActiveSagaInstance sagaInstanceState)
        {
            string sagaType;

            if (context.IncomingLogicalMessage.Headers.ContainsKey(Headers.SagaId) &&
                context.IncomingLogicalMessage.Headers.TryGetValue(Headers.SagaType, out sagaType))
            {
                //we want to move away from the assembly fully qualified name since that will break if you move sagas
                // between assemblies. We use the fullname instead which is enough to identify the saga
                if (sagaType.StartsWith(sagaInstanceState.SagaType.FullName))
                {
                    //so now we have a saga id for this saga and if we can't find it we shouldn't start a new one
                    return false;
                }
            }

            return SagaConfigurationCache.IsAStartSagaMessage(sagaInstanceState.SagaType, context.IncomingLogicalMessage.MessageType);
        }

        [ObsoleteEx(RemoveInVersion = "6.0", TreatAsErrorFromVersion = "5.1", Message = "Enriching the headers for saga related information has been moved to the SagaAudit plugin in ServiceControl.  Add a reference to the Saga audit plugin in your endpoint to get more information.")]
        void LogSaga(ActiveSagaInstance saga, IncomingContext context)
        {

            var audit = string.Format("{0}:{1}", saga.Metadata.Name, saga.SagaId);

            string header;

            if (context.IncomingLogicalMessage.Headers.TryGetValue(Headers.InvokedSagas, out header))
            {
                context.IncomingLogicalMessage.Headers[Headers.InvokedSagas] += string.Format(";{0}", audit);
            }
            else
            {
                context.IncomingLogicalMessage.Headers[Headers.InvokedSagas] = audit;
            }
        }

        void InvokeSagaNotFoundHandlers(Type sagaType)
        {
            logger.InfoFormat("Could not find a saga of type '{0}' for the message type '{1}'. Going to invoke SagaNotFoundHandlers.", sagaType.FullName, currentContext.IncomingLogicalMessage.MessageType.FullName);

            foreach (var handler in currentContext.Builder.BuildAll<IHandleSagaNotFound>())
            {
                logger.DebugFormat("Invoking SagaNotFoundHandler: {0}", handler.GetType().FullName);
                handler.Handle(currentContext.IncomingLogicalMessage.Instance);
            }
        }

        static bool IsTimeoutMessage(LogicalMessage message)
        {
            string isSagaTimeout;

            if (message.Headers.TryGetValue(Headers.IsSagaTimeoutMessage, out isSagaTimeout))
            {
                return true;
            }

            string version;

            if (!message.Headers.TryGetValue(Headers.NServiceBusVersion, out version))
            {
                return false;
            }

            if (!version.StartsWith("3"))
            {
                return false;
            }

            string sagaId;
            if (message.Headers.TryGetValue(Headers.SagaId, out sagaId))
            {
                if (string.IsNullOrEmpty(sagaId))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            string expire;
            if (message.Headers.TryGetValue(TimeoutManagerHeaders.Expire, out expire))
            {
                if (string.IsNullOrEmpty(expire))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            message.Headers[Headers.IsSagaTimeoutMessage] = Boolean.TrueString;
            return true;
        }

        IContainSagaData TryLoadSagaEntity(SagaMetadata metadata, LogicalMessage message)
        {

            var sagaEntityType = (Type)metadata.Properties["entity-clr-type"];

            string sagaId;

            //todo - we should check saga type as well, add failing test first
            if (message.Headers.TryGetValue(Headers.SagaId, out sagaId) && !string.IsNullOrEmpty(sagaId))
            {
                //since we have a saga id available we can now shortcut the finders and just load the saga
                var loaderType = typeof(LoadSagaByIdWrapper<>).MakeGenericType(sagaEntityType);

                var loader = (SagaLoader)Activator.CreateInstance(loaderType);

                return loader.Load(SagaPersister, sagaId);
            }

            var finderDefinition = metadata.GetFinder(message.MessageType.FullName);

            var finderType = finderDefinition.Type;

            var finder = currentContext.Builder.Build(finderType);

            return ((SagaFinder)finder).Find(currentContext.Builder, finderDefinition, message);
        }

        void NotifyTimeoutManagerThatSagaHasCompleted(Saga.Saga saga)
        {
            MessageDeferrer.ClearDeferredMessages(Headers.SagaId, saga.Entity.Id.ToString());
        }

        IContainSagaData CreateNewSagaEntity(SagaMetadata metadata,LogicalMessage message)
        {
            var sagaEntityType = (Type)metadata.Properties["entity-clr-type"];

            var sagaEntity = (IContainSagaData)Activator.CreateInstance(sagaEntityType);

            //todo -make pluggable
            sagaEntity.Id = CombGuid.Generate();
            sagaEntity.OriginalMessageId = message.Headers[Headers.MessageId];

            string replyToAddress;

            if (message.Headers.TryGetValue(Headers.ReplyToAddress, out replyToAddress))
            {
                sagaEntity.Originator = replyToAddress;
            }

            return sagaEntity;
        }

        IncomingContext currentContext;

        static ILog logger = LogManager.GetLogger<SagaPersistenceBehavior>();

        public class Registration : RegisterStep
        {
            public Registration()
                : base(WellKnownStep.InvokeSaga, typeof(SagaPersistenceBehavior), "Invokes the saga logic")
            {
                InsertBefore(WellKnownStep.InvokeHandlers);
                InsertAfter("SetCurrentMessageBeingHandled");
            }
        }
    }

    abstract class SagaFinder
    {
        internal abstract IContainSagaData Find(IBuilder builder,SagaFinderDefinition finderDefinition, LogicalMessage message);
    }
}