namespace NServiceBus.Sagas
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.Serialization;
    using NServiceBus.Saga;
    using NServiceBus.Utils.Reflection;

    class TypeBasedSagaMetaModel
    {
        public static IEnumerable<SagaMetadata> Create(IList<Type> availableTypes, Conventions conventions)
        {
            return availableTypes.Where(IsSagaType)
                .Select(t => Create(t, availableTypes, conventions)).ToList();
        }

        static bool IsSagaType(Type t)
        {
            return typeof(Saga).IsAssignableFrom(t) && t != typeof(Saga) && !t.IsGenericType;
        }

        public static SagaMetadata Create(Type sagaType)
        {
            return Create(sagaType, new List<Type>(), new Conventions());
        }
        public static SagaMetadata Create(Type sagaType, IEnumerable<Type> availableTypes, Conventions conventions)
        {
            if (!IsSagaType(sagaType))
            {
                throw new Exception(sagaType.FullName + " is not a saga");
            }

            var sagaEntityType = sagaType.BaseType.GetGenericArguments().Single();

            var uniquePropertiesOnEntity = FindUniqueAttributes(sagaEntityType).ToList();

            var mapper = new SagaMapper();

            var saga = (Saga)FormatterServices.GetUninitializedObject(sagaType);
            saga.ConfigureHowToFindSaga(mapper);

            ApplyScannedFinders(mapper, sagaEntityType, availableTypes, conventions);


            var finders = new List<SagaFinderDefinition>();

            foreach (var mapping in mapper.Mappings)
            {
                uniquePropertiesOnEntity.Add(mapping.SagaPropName);

                SetFinderForMessage(mapping, sagaEntityType, finders);
            }

            var associatedMessages = GetAssociatedMessages(sagaType)
                .ToList();

            //foreach (var associatedMessage in associatedMessages)
            //{
            //    if (!associatedMessage.IsAllowedToStartSaga)
            //    {
            //        continue;
            //    }

            //    // var finder = finders.SingleOrDefault(f => f.MessageType == associatedMessage.MessageType);

            //    //if (finder == null)
            //    //{
            //    //    throw new Exception(string.Format("All messages starting a saga needs to have a configured finder. Please add a mapping for message: '{0}' to saga type '{1}'",associatedMessage.MessageType,sagaType.FullName));
            //    //}
            //}

            var metadata = new SagaMetadata(associatedMessages, finders)
            {
                Name = sagaType.FullName,
                EntityName = sagaEntityType.FullName,
                UniqueProperties = uniquePropertiesOnEntity.Distinct(),

            };

            metadata.Properties.Add("entity-clr-type", sagaEntityType);
            metadata.Properties.Add("saga-clr-type", sagaType);

            return metadata;
        }

        static void ApplyScannedFinders(SagaMapper mapper, Type sagaEntityType, IEnumerable<Type> availableTypes, Conventions conventions)
        {
            var actualFinders = availableTypes.Where(t => typeof(IFinder).IsAssignableFrom(t))
                .ToList();

            foreach (var finderType in actualFinders)
            {
                foreach (var interfaceType in finderType.GetInterfaces())
                {
                    var args = interfaceType.GetGenericArguments();
                    if (args.Length != 2)
                    {
                        continue;
                    }

                    Type messageType = null;
                    Type entityType = null;
                    foreach (var type in args)
                    {

                        if (typeof(IContainSagaData).IsAssignableFrom(type))
                        {
                            entityType = type;
                        }

                        if (conventions.IsMessageType(type) || type == typeof(object))
                        {
                            messageType = type;
                        }
                    }

                    if (entityType == null || messageType == null || entityType != sagaEntityType)
                    {
                        continue;
                    }

                    var existingMapping = mapper.Mappings.SingleOrDefault(m => m.MessageType == messageType);

                    if (existingMapping != null)
                    {
                        existingMapping.CustomFinderType = finderType;
                    }
                    else
                    {
                        mapper.ConfigureCustomFinder(finderType, messageType);
                    }


                }
            }
        }

        static void SetFinderForMessage(SagaToMessageMap mapping, Type sagaEntityType, List<SagaFinderDefinition> finders)
        {
            var finder = new SagaFinderDefinition
            {
                MessageType = mapping.MessageType.FullName
            };

            if (mapping.CustomFinderType != null)
            {
                finder.Type = typeof(CustomFinderAdapter<,>).MakeGenericType(sagaEntityType, mapping.MessageType);

                finder.Properties["custom-finder-clr-type"] = mapping.CustomFinderType;
            }
            else
            {
                finder.Type = typeof(PropertySagaFinder<>).MakeGenericType(sagaEntityType);
                finder.Properties["property-accessor"] = mapping.MessageProp;
                finder.Properties["saga-property-name"] = mapping.SagaPropName;

            }

            finders.Add(finder);
        }

        static IEnumerable<SagaMessage> GetAssociatedMessages(Type sagaType)
        {
            var result = GetMessagesCorrespondingToFilterOnSaga(sagaType, typeof(IAmStartedByMessages<>))
                .Select(t => new SagaMessage(t.FullName, true)).ToList();

            foreach (var messageType in GetMessagesCorrespondingToFilterOnSaga(sagaType, typeof(IHandleMessages<>)))
            {
                if (result.Any(m => m.MessageType == messageType.FullName))
                {
                    continue;
                }
                result.Add(new SagaMessage(messageType.FullName, false));
            }

            foreach (var messageType in GetMessagesCorrespondingToFilterOnSaga(sagaType, typeof(IHandleTimeouts<>)))
            {
                if (result.Any(m => m.MessageType == messageType.FullName))
                {
                    continue;
                }
                result.Add(new SagaMessage(messageType.FullName, false));
            }

            return result;
        }

        static IEnumerable<Type> GetMessagesCorrespondingToFilterOnSaga(Type sagaType, Type filter)
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
                    yield return argument;
                }
            }
        }

        static IEnumerable<string> FindUniqueAttributes(Type sagaEntityType)
        {
            return UniqueAttribute.GetUniqueProperties(sagaEntityType).Select(pt => pt.Name);
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
                    MessageType = typeof(TMessage)
                });
            }

            public void ConfigureCustomFinder(Type finderType, Type messageType)
            {
                Mappings.Add(new SagaToMessageMap
                {
                    MessageType = messageType,
                    CustomFinderType = finderType
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
}