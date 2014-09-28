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
        public void FindSagasByEntityName()
        {
            var model = TypeBasedSagaMetaModel.Create(new []{typeof(MySaga)});

            var metadata = model.FindByEntityName(typeof(MyEntity).FullName);


            Assert.NotNull(metadata);
        }

        [Test]
        public void FindUniquePropertiesByAttribute()
        {
            var model = TypeBasedSagaMetaModel.Create(new[] { typeof(MySaga) });

            var metadata = model.FindByEntityName(typeof(MyEntity).FullName);


            Assert.AreEqual("UniqueProperty",metadata.UniqueProperties.Single());
        }


        [Test]
        public void FilterOutNonSagaTypes()
        {
            Assert.AreEqual(1,TypeBasedSagaMetaModel.Create(new []{typeof(MySaga),typeof(string)}).All.Count());
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
}