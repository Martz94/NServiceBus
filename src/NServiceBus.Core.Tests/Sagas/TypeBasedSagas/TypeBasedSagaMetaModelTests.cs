namespace NServiceBus.Core.Tests.Sagas.TypeBasedSagas
{
    using System.Linq;
    using NServiceBus.Features;
    using NServiceBus.Saga;
    using NUnit.Framework;

    [TestFixture]
    public class TypeBasedSagaMetaModelTests
    {
        [Test]
        public void FindSagasByName()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySaga) });

            var metadata = model.FindByName(typeof(MySaga).FullName);


            Assert.NotNull(metadata);
        }
        [Test]
        public void FindSagasByEntityName()
        {
            var model = TypeBasedSagaMetaModel.Create(new []{typeof(MySaga)});

            var metadata = model.FindByEntityName(typeof(MyEntity).FullName);


            Assert.NotNull(metadata);
        }

        [Test]
        public void GetEntityClrType()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySaga) });

            var metadata = model.FindByEntityName(typeof(MyEntity).FullName);


            Assert.AreEqual(typeof(MyEntity), metadata.Properties["entity-clr-type"]);
        }

        [Test]
        public void DetectUniquePropertiesByAttribute()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySaga) });

            var metadata = model.FindByEntityName(typeof(MyEntity).FullName);


            Assert.AreEqual("UniqueProperty",metadata.UniqueProperties.Single());
        }
        [Test]
        public void AutomaticallyAddUniqueForMappedProperties()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySagaWithMappedProperty) });

            var metadata = model.FindByEntityName(typeof(MySagaWithMappedPropertyData).FullName);


            Assert.AreEqual("UniqueProperty", metadata.UniqueProperties.Single());
        }


        [Test]
        public void FilterOutNonSagaTypes()
        {
            Assert.AreEqual(1,TypeBasedSagaMetaModel.Create(new []{typeof(MySaga),typeof(string)}).All.Count());
        }

        class MySagaWithMappedProperty : Saga<MySagaWithMappedPropertyData>
        {
            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaWithMappedPropertyData> mapper)
            {
                mapper.ConfigureMapping<SomeMessage>(m=>m.SomeProperty)
                    .ToSaga(s=>s.UniqueProperty);
            }
        }

        class MySagaWithMappedPropertyData : ContainSagaData
        {
            public int UniqueProperty { get; set; }
        }

        class MySaga:Saga<MyEntity>
        {
            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MyEntity> mapper)
            {
                
            }
        }

        class MyEntity : ContainSagaData
        {
            [Unique]
            public int UniqueProperty { get; set; }
        }
    }

    class SomeMessage
    {
        public int SomeProperty{ get; set; }
    }
}