namespace NServiceBus.Core.Tests.Sagas.TypeBasedSagas
{
    using NServiceBus.Features;
    using NServiceBus.Saga;
    using NUnit.Framework;

    [TestFixture]
    public class TypeBasedSagaMetaModelTests
    {
        [Test]
        public void ParseSagaEntities()
        {
            var model = TypeBasedSagaMetaModel.Create<MySaga>();

            var metadata = model.FindByEntityName(typeof(MyEntity).FullName);


            Assert.NotNull(metadata);
        }
        class MySaga:Saga<MyEntity>
        {
            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MyEntity> mapper)
            {
                
            }
        }

        class MyEntity:ContainSagaData
        { }
    }
}