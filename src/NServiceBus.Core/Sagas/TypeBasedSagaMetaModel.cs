namespace NServiceBus.Features
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.Serialization;
    using NServiceBus.Saga;
    using NServiceBus.Sagas;
    using NServiceBus.Utils.Reflection;

    class TypeBasedSagaMetaModel : ISagaMetaModel
    {
        readonly Dictionary<string, SagaMetadata> model = new Dictionary<string, SagaMetadata>();

        public static ISagaMetaModel Create(IList<Type> availableTypes)
        {
            return new TypeBasedSagaMetaModel(availableTypes.Where(t => typeof(Saga).IsAssignableFrom(t) && t != typeof(Saga) && !t.IsGenericType)
                .Select(GenerateModel).ToList());
        }
        static SagaMetadata GenerateModel(Type sagaType)
        {
            var sagaEntityType = sagaType.BaseType.GetGenericArguments().Single();

            var uniquePropertiesOnEntity = FindUniqueAttributes(sagaEntityType).ToList();

            var mapper = new SagaMapper();

            var saga = (Saga)FormatterServices.GetUninitializedObject(sagaType);
            saga.ConfigureHowToFindSaga(mapper);

            foreach (var mapping in mapper.Mappings)
            {
                uniquePropertiesOnEntity.Add(mapping.SagaPropName);
            }

            var associatedMessages = GetAssociatedMessages(sagaType,new Conventions());

            var metadata = new SagaMetadata(associatedMessages)
            {
                Name = sagaType.FullName,
                EntityName = sagaEntityType.FullName,
                UniqueProperties = uniquePropertiesOnEntity.Distinct(),
                
            };

            metadata.Properties.Add("entity-clr-type",sagaEntityType);
            metadata.Properties.Add("saga-clr-type", sagaType);

            return metadata;
        }

        static IEnumerable<SagaMessage> GetAssociatedMessages(Type sagaType,Conventions conventions)
        {
            var result =  GetMessagesCorrespondingToFilterOnSaga(sagaType, typeof(IAmStartedByMessages<>), conventions)
                .Select(t=>new SagaMessage(t.FullName,true)).ToList();

            foreach (var messageType in GetMessagesCorrespondingToFilterOnSaga(sagaType, typeof(IHandleMessages<>), conventions))
            {
                if (result.Any(m=>m.MessageType == messageType.FullName))
                {
                    continue;
                }
                result.Add(new SagaMessage(messageType.FullName,false));
            }

            return result;
        }

        static IEnumerable<Type> GetMessagesCorrespondingToFilterOnSaga(Type sagaType, Type filter, Conventions conventions)
        {
            foreach (var interfaceType in sagaType.GetInterfaces())
            {
                foreach (var argument in interfaceType.GetGenericArguments())
                {
                    var genericType = filter.MakeGenericType(argument);
                    var isOfFilterType = genericType == interfaceType;
                    if (!isOfFilterType)
                    {
                        continue;
                    }
                    if (conventions.IsMessageType(argument))
                    {
                        yield return argument;
                        continue;
                    }
                    var message = string.Format("The saga '{0}' implements '{1}' but the message type '{2}' is not classified as a message. You should either use 'Unobtrusive Mode Messages' or the message should implement either 'IMessage', 'IEvent' or 'ICommand'.", sagaType.FullName, genericType.Name, argument.FullName);
                    throw new Exception(message);
                }
            }
        }

        static IEnumerable<string> FindUniqueAttributes(Type sagaEntityType)
        {
            return UniqueAttribute.GetUniqueProperties(sagaEntityType).Select(pt => pt.Name);
        }

        private TypeBasedSagaMetaModel(List<SagaMetadata> metadata)

        {
            foreach (var sagaMetaData in metadata)
            {
                model[sagaMetaData.EntityName] = sagaMetaData;
            }
        }

        public SagaMetadata FindByEntityName(string name)
        {
            return model[name];
        }

        public IEnumerable<SagaMetadata> All
        {
            get { return model.Values; }
        }

        public SagaMetadata FindByName(string name)
        {
            //todo - add a more efficient lookup
            return model.Values.Single(m => m.Name == name);
        }

        public IEnumerable<SagaMetadata> FindByMessageType(string messageTypeId)
        {
            //todo
            return model.Values;
        }

        class SagaMapper : IConfigureHowToFindSagaWithMessage
        {
            public List<SagaToMessageMap> Mappings = new List<SagaToMessageMap>();

            void IConfigureHowToFindSagaWithMessage.ConfigureMapping<TSagaEntity, TMessage>(Expression<Func<TSagaEntity, object>> sagaEntityProperty, Expression<Func<TMessage, object>> messageExpression)
            {
                var sagaProp = Reflect<TSagaEntity>.GetProperty(sagaEntityProperty, true);

                ThrowIfNotPropertyLambdaExpression(sagaEntityProperty, sagaProp);
                var compiledMessageExpression = messageExpression.Compile();
                var messageFunc = new Func<object, object>(o => compiledMessageExpression((TMessage)o));

                Mappings.Add(new SagaToMessageMap
                {
                    MessageProp = messageFunc,
                    SagaPropName = sagaProp.Name,
                });
            }

            // ReSharper disable once UnusedParameter.Local
            void ThrowIfNotPropertyLambdaExpression<TSagaEntity>(Expression<Func<TSagaEntity, object>> expression, PropertyInfo propertyInfo)
            {
                if (propertyInfo == null)
                {
                    throw new ArgumentException(
                        String.Format(
                            "Only public properties are supported for mapping Sagas. The lambda expression provided '{0}' is not mapping to a Property!",
                            expression.Body));
                }
            }
        }

    }

    interface ISagaMetaModel
    {
        SagaMetadata FindByEntityName(string name);

        IEnumerable<SagaMetadata> All { get; }
        SagaMetadata FindByName(string name);
        IEnumerable<SagaMetadata> FindByMessageType(string messageTypeId);
    }

    /// <summary>
    /// Contains metadata for known sagas
    /// </summary>
    public class SagaMetadata
    { 
        internal SagaMetadata(IEnumerable<SagaMessage> messages)
        {
            Properties = new Dictionary<string, object>();
            UniqueProperties = new List<string>();

            associatedMessages = new Dictionary<string, SagaMessage>();

            foreach (var sagaMessage in messages)
            {
                associatedMessages[sagaMessage.MessageType] = sagaMessage;
            }
        }

        Dictionary<string, SagaMessage> associatedMessages;
        /// <summary>
        /// Unique properties for this saga
        /// </summary>
        public IEnumerable<string> UniqueProperties;

        /// <summary>
        /// The name of the saga
        /// </summary>
        public string Name;

        /// <summary>
        /// List of related properties
        /// </summary>
        public Dictionary<string, object> Properties;

        /// <summary>
        /// The name of the saga data entity
        /// </summary>
        public string EntityName;

        /// <summary>
        /// True if the specified message type is allowed to start the saga
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        public bool IsMessageAllowedToStartTheSaga(string messageType)
        {
            SagaMessage sagaMessage;

            if (!associatedMessages.TryGetValue(messageType, out sagaMessage))
            {
                return false;
            }
            return sagaMessage.IsAllowedToStartSaga;
        }

        /// <summary>
        /// Returns the list of messages that is associated with this saga
        /// </summary>
        public IEnumerable<SagaMessage> AssociatedMessages
        {
            get { return associatedMessages.Values; }
        }
    }

    /// <summary>
    /// Representation of a message that is related to a saga
    /// </summary>
    public class SagaMessage
    {
        /// <summary>
        /// True if the message can start the saga
        /// </summary>
        public readonly bool IsAllowedToStartSaga;

        /// <summary>
        /// The type of the message
        /// </summary>
        public readonly string MessageType;

        internal SagaMessage(string messageType, bool isAllowedToStart)
        {
            MessageType = messageType;
            IsAllowedToStartSaga = isAllowedToStart;
        }
    }
}