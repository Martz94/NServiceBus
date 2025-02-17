namespace NServiceBus.Saga
{
    using System;

    /// <summary>
    /// This class is used to define sagas containing data and handling a message.
    /// To handle more message types, implement <see cref="IHandleMessages{T}"/>
    /// for the relevant types.
    /// To signify that the receipt of a message should start this saga,
    /// implement <see cref="IAmStartedByMessages{T}"/> for the relevant message type.
    /// </summary>
    public abstract class Saga 
    {
        /// <summary>
        /// The saga's typed data.
        /// </summary>
        public IContainSagaData Entity{get; set; }

        /// <summary>
        /// Override this method in order to configure how this saga's data should be found.
        /// </summary>
        internal protected abstract void ConfigureHowToFindSaga(IConfigureHowToFindSagaWithMessage sagaMessageFindingConfiguration);

        /// <summary>
        /// Bus object used for retrieving the sender endpoint which caused this saga to start.
        /// Necessary for <see cref="ReplyToOriginator" />.
        /// </summary>
        public IBus Bus
        {
            get
            {
                if (bus == null)
                    throw new InvalidOperationException("No IBus instance available, please configure one and also verify that you're not defining your own Bus property in your saga since that hides the one in the base class");

                return bus;
            }
            set { bus = value; }
        }

        IBus bus;

        /// <summary>
        /// Indicates that the saga is complete.
        /// In order to set this value, use the <see cref="MarkAsComplete" /> method.
        /// </summary>
        public bool Completed { get; private set; }

        /// <summary>
        /// Request for a timeout to occur at the given <see cref="DateTime"/>.
        /// </summary>
        /// <param name="at"><see cref="DateTime"/> to send timeout <typeparamref name="TTimeoutMessageType"/>.</param>
        protected void RequestTimeout<TTimeoutMessageType>(DateTime at) where TTimeoutMessageType : new()
        {
            RequestTimeout(at, new TTimeoutMessageType());
        }

        /// <summary>
        /// Request for a timeout to occur at the given <see cref="DateTime"/>.
        /// </summary>
        /// <param name="at"><see cref="DateTime"/> to send call <paramref name="action"/>.</param>
        /// <param name="action">Callback to execute after <paramref name="at"/> is reached.</param>
        protected void RequestTimeout<TTimeoutMessageType>(DateTime at, Action<TTimeoutMessageType> action) where TTimeoutMessageType : new()
        {
            var instance = new TTimeoutMessageType();
            action(instance);
            RequestTimeout(at, instance);
        }

        /// <summary>
        /// Request for a timeout to occur at the given <see cref="DateTime"/>.
        /// </summary>
        /// <param name="at"><see cref="DateTime"/> to send timeout <paramref name="timeoutMessage"/>.</param>
        /// <param name="timeoutMessage">The message to send after <paramref name="at"/> is reached.</param>
        protected void RequestTimeout<TTimeoutMessageType>(DateTime at, TTimeoutMessageType timeoutMessage) where TTimeoutMessageType : new()
        {
            if (at.Kind == DateTimeKind.Unspecified)
            {
                throw new InvalidOperationException("Kind property of DateTime 'at' must be specified.");
            }

            VerifySagaCanHandleTimeout(timeoutMessage);
            SetTimeoutHeaders(timeoutMessage);

            Bus.Defer(at, timeoutMessage);
        }

        void VerifySagaCanHandleTimeout<TTimeoutMessageType>(TTimeoutMessageType timeoutMessage) where TTimeoutMessageType : new()
        {
            var canHandleTimeoutMessage = this is IHandleTimeouts<TTimeoutMessageType>;
            if (!canHandleTimeoutMessage)
            {
                var message = string.Format("The type '{0}' cannot request timeouts for '{1}' because it does not implement 'IHandleTimeouts<{2}>'", GetType().Name, timeoutMessage, typeof(TTimeoutMessageType).FullName);
                throw new Exception(message);
            }
        }

        /// <summary>
        /// Request for a timeout to occur within the give <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="within">Given <see cref="TimeSpan"/> to delay timeout message by.</param>
        protected void RequestTimeout<TTimeoutMessageType>(TimeSpan within) where TTimeoutMessageType : new()
        {
            RequestTimeout(within, new TTimeoutMessageType());
        }

        /// <summary>
        /// Request for a timeout to occur within the give <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="within">Given <see cref="TimeSpan"/> to delay timeout message by.</param>
        /// <param name="messageConstructor">An <see cref="Action"/> which initializes properties of the message that is sent after <paramref name="within"/> expires.</param>
        protected void RequestTimeout<TTimeoutMessageType>(TimeSpan within, Action<TTimeoutMessageType> messageConstructor) where TTimeoutMessageType : new()
        {
            var instance = new TTimeoutMessageType();
            messageConstructor(instance);
            RequestTimeout(within, instance);
        }

        /// <summary>
        /// Request for a timeout to occur within the given <see cref="TimeSpan"/>.
        /// </summary>
        /// <param name="within">Given <see cref="TimeSpan"/> to delay timeout message by.</param>
        /// <param name="timeoutMessage">The message to send after <paramref name="within"/> expires.</param>
        protected void RequestTimeout<TTimeoutMessageType>(TimeSpan within, TTimeoutMessageType timeoutMessage) where TTimeoutMessageType : new()
        {
            VerifySagaCanHandleTimeout(timeoutMessage);
            SetTimeoutHeaders(timeoutMessage);

            Bus.Defer(within, timeoutMessage);
        }


        void SetTimeoutHeaders(object toSend)
        {
            Bus.SetMessageHeader(toSend, Headers.SagaId, Entity.Id.ToString());
            Bus.SetMessageHeader(toSend, Headers.IsSagaTimeoutMessage, Boolean.TrueString);
            Bus.SetMessageHeader(toSend, Headers.SagaType, GetType().AssemblyQualifiedName);
        }

        /// <summary>
        /// Sends the <paramref name="message"/> using the bus to the endpoint that caused this saga to start.
        /// </summary>
        protected virtual void ReplyToOriginator(object message)
        {
            if (string.IsNullOrEmpty(Entity.Originator))
            {
                throw new Exception("Entity.Originator cannot be null. Perhaps the sender is a SendOnly endpoint.");
            }
            Bus.Send(Entity.Originator, Entity.OriginalMessageId, message);
        }

        /// <summary>
        /// Instantiates a message of the given type, setting its properties using the given action,
        /// and sends it using the bus to the endpoint that caused this saga to start.
        /// </summary>
        /// <typeparam name="TMessage">The type of message to construct.</typeparam>
        /// <param name="messageConstructor">An <see cref="Action"/> which initializes properties of the message reply with.</param>
        protected virtual void ReplyToOriginator<TMessage>(Action<TMessage> messageConstructor) where TMessage : new()
        {
            if (messageConstructor != null)
            {
                var instance = new TMessage();
                messageConstructor(instance);
                ReplyToOriginator(instance);
            }
            else
            {
                ReplyToOriginator(null);
            }
        }

        /// <summary>
        /// Marks the saga as complete.
        /// This may result in the sagas state being deleted by the persister.
        /// </summary>
        protected virtual void MarkAsComplete()
        {
            Completed = true;
        }

    }
}
