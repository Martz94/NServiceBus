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
        readonly Dictionary<string, SagaMetaData> model = new Dictionary<string, SagaMetaData>();

        public static ISagaMetaModel Create(IList<Type> availableTypes)
        {
            return new TypeBasedSagaMetaModel(availableTypes.Where(t => typeof(Saga).IsAssignableFrom(t) && t != typeof(Saga) && !t.IsGenericType)
                .Select(GenerateModel).ToList());
        }
        static SagaMetaData GenerateModel(Type sagaType)
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

            return new SagaMetaData
            {
                EntityName = sagaEntityType.FullName,
                UniqueProperties = uniquePropertiesOnEntity.Distinct()
            };
        }

        static IEnumerable<string> FindUniqueAttributes(Type sagaEntityType)
        {
            return UniqueAttribute.GetUniqueProperties(sagaEntityType).Select(pt => pt.Name);
        }

        private TypeBasedSagaMetaModel(List<SagaMetaData> metadata)
        {
            foreach (var sagaMetaData in metadata)
            {
                model[sagaMetaData.EntityName] = sagaMetaData;
            }
        }

        public SagaMetaData FindByEntityName(string name)
        {
            return model[name];
        }

        public IEnumerable<SagaMetaData> All
        {
            get { return model.Values; }
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
        SagaMetaData FindByEntityName(string name);

        IEnumerable<SagaMetaData> All { get; }
    }

    class SagaMetaData
    {
        public IEnumerable<string> UniqueProperties;
        public string EntityName;
    }
}