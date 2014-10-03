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

            var metadata = new SagaMetadata
            {
                Name = sagaType.FullName,
                EntityName = sagaEntityType.FullName,
                UniqueProperties = uniquePropertiesOnEntity.Distinct()
            };

            metadata.Properties.Add("entity-clr-type",sagaEntityType);

            return metadata;
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

    class SagaMetadata
    { 
        public SagaMetadata()
        {
            Properties = new Dictionary<string, object>();
            UniqueProperties = new List<string>();
        }
        public IEnumerable<string> UniqueProperties;
        public string Name;
        public Dictionary<string, object> Properties;
        public string EntityName;
    }
}