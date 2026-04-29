using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenRiaServices.Server;
using OpenRiaServices.Server.UnitTesting;

namespace OpenRiaServices.Server.Test
{
    [TestClass]
    public class SoftDeleteTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
        }

        [TestMethod]
        public void SoftDeleteAttribute_DefaultValues()
        {
            var attribute = new SoftDeleteAttribute();
            Assert.AreEqual("IsDeleted", attribute.PropertyName);
            Assert.IsTrue(attribute.AutoFilterEnabled);
        }

        [TestMethod]
        public void SoftDeleteAttribute_CustomPropertyName()
        {
            var attribute = new SoftDeleteAttribute("Deleted");
            Assert.AreEqual("Deleted", attribute.PropertyName);
            Assert.IsTrue(attribute.AutoFilterEnabled);
        }

        [TestMethod]
        public void SoftDeleteAttribute_CanDisableAutoFilter()
        {
            var attribute = new SoftDeleteAttribute
            {
                AutoFilterEnabled = false
            };
            Assert.AreEqual("IsDeleted", attribute.PropertyName);
            Assert.IsFalse(attribute.AutoFilterEnabled);
        }

        [TestMethod]
        public void MetaType_RecognizesSoftDeleteAttribute()
        {
            MetaType metaType = MetaType.GetMetaType(typeof(SoftDeleteEntity));
            Assert.IsTrue(metaType.IsSoftDeleteEnabled);
            Assert.IsNotNull(metaType.SoftDeleteConfiguration);
            Assert.AreEqual("IsDeleted", metaType.SoftDeleteConfiguration.PropertyName);
        }

        [TestMethod]
        public void MetaType_RecognizesCustomSoftDeleteAttribute()
        {
            MetaType metaType = MetaType.GetMetaType(typeof(CustomSoftDeleteEntity));
            Assert.IsTrue(metaType.IsSoftDeleteEnabled);
            Assert.IsNotNull(metaType.SoftDeleteConfiguration);
            Assert.AreEqual("Deleted", metaType.SoftDeleteConfiguration.PropertyName);
        }

        [TestMethod]
        public void MetaType_NoSoftDeleteAttribute()
        {
            MetaType metaType = MetaType.GetMetaType(typeof(RegularEntity));
            Assert.IsFalse(metaType.IsSoftDeleteEnabled);
            Assert.IsNull(metaType.SoftDeleteConfiguration);
        }

        [TestMethod]
        public void SoftDeleteConfiguration_BoolType_UsesTrueAsDeletedValue()
        {
            var entity = new SoftDeleteEntity { Id = 1, Name = "Test" };
            MetaType metaType = MetaType.GetMetaType(typeof(SoftDeleteEntity));
            
            Assert.IsFalse(entity.IsDeleted);
            metaType.SoftDeleteConfiguration.MarkAsDeleted(entity);
            Assert.IsTrue(entity.IsDeleted);
        }

        [TestMethod]
        public void SoftDeleteConfiguration_IntType_UsesOneAsDeletedValue()
        {
            var entity = new IntSoftDeleteEntity { Id = 1, Name = "Test" };
            MetaType metaType = MetaType.GetMetaType(typeof(IntSoftDeleteEntity));
            
            Assert.AreEqual(0, entity.IsDeleted);
            metaType.SoftDeleteConfiguration.MarkAsDeleted(entity);
            Assert.AreEqual(1, entity.IsDeleted);
        }

        [TestMethod]
        public async Task DeleteOperation_ConvertsToUpdate_ForSoftDeleteEntity()
        {
            var host = new DomainServiceTestHost<SoftDeleteTestDomainService>(() => new SoftDeleteTestDomainService());
            
            var entity = new SoftDeleteEntity { Id = 1, Name = "Test" };
            host.Insert(entity);

            Assert.IsFalse(entity.IsDeleted);
            
            host.Delete(entity);
            
            Assert.IsTrue(entity.IsDeleted, "Entity should be marked as deleted");
        }

        [TestMethod]
        public async Task Query_Default_ExcludesSoftDeletedEntities()
        {
            var host = new DomainServiceTestHost<SoftDeleteTestDomainService>(() => new SoftDeleteTestDomainService());
            
            var entity1 = new SoftDeleteEntity { Id = 1, Name = "Active" };
            var entity2 = new SoftDeleteEntity { Id = 2, Name = "Deleted", IsDeleted = true };
            
            host.Insert(entity1);
            host.Insert(entity2);

            var results = host.Query<SoftDeleteEntity>(ds => ds.GetEntities());
            
            Assert.AreEqual(1, results.Count());
            Assert.AreEqual(1, results.First().Id);
        }

        [TestMethod]
        public async Task QueryWithDeleted_IncludesSoftDeletedEntities()
        {
            var host = new DomainServiceTestHost<SoftDeleteTestDomainService>(() => new SoftDeleteTestDomainService());
            
            var entity1 = new SoftDeleteEntity { Id = 1, Name = "Active" };
            var entity2 = new SoftDeleteEntity { Id = 2, Name = "Deleted", IsDeleted = true };
            
            host.Insert(entity1);
            host.Insert(entity2);

            var results = host.QueryWithDeleted<SoftDeleteEntity>(ds => ds.GetEntities());
            
            Assert.AreEqual(2, results.Count());
        }

        [TestMethod]
        public void DeleteOperation_RegularEntity_NotConverted()
        {
            var host = new DomainServiceTestHost<SoftDeleteTestDomainService>(() => new SoftDeleteTestDomainService());
            
            var entity = new RegularEntity { Id = 1, Name = "Test" };
            host.Insert(entity);
            
            host.Delete(entity);
            
            var results = host.Query<RegularEntity>(ds => ds.GetRegularEntities());
            Assert.AreEqual(0, results.Count());
        }

        [TestMethod]
        public void OnSoftDelete_CanBeOverridden()
        {
            var host = new DomainServiceTestHost<CustomSoftDeleteTestDomainService>(() => new CustomSoftDeleteTestDomainService());
            
            var entity = new SoftDeleteEntity { Id = 1, Name = "Test" };
            host.Insert(entity);

            host.Delete(entity);
            
            Assert.IsTrue(entity.IsDeleted);
            Assert.AreEqual("CustomReason", entity.DeleteReason);
        }

        [TestMethod]
        public void QueryDescription_IncludeDeleted_Flag()
        {
            var description = DomainServiceDescription.GetDescription(typeof(SoftDeleteTestDomainService));
            var queryMethod = description.GetQueryMethod("GetEntities");
            
            var queryDesc = new QueryDescription(queryMethod, Array.Empty<object>(), includeDeleted: true);
            
            Assert.IsTrue(queryDesc.IncludeDeleted);
        }

        [TestMethod]
        public void QueryDescription_Default_IncludeDeleted_IsFalse()
        {
            var description = DomainServiceDescription.GetDescription(typeof(SoftDeleteTestDomainService));
            var queryMethod = description.GetQueryMethod("GetEntities");
            
            var queryDesc = new QueryDescription(queryMethod, Array.Empty<object>());
            
            Assert.IsFalse(queryDesc.IncludeDeleted);
        }
    }

    [SoftDelete]
    public class SoftDeleteEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsDeleted { get; set; }
        public string DeleteReason { get; set; }
    }

    [SoftDelete("Deleted")]
    public class CustomSoftDeleteEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Deleted { get; set; }
    }

    [SoftDelete]
    public class IntSoftDeleteEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public int IsDeleted { get; set; }
    }

    public class RegularEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
    }

    [EnableClientAccess]
    public class SoftDeleteTestDomainService : DomainService
    {
        private readonly List<SoftDeleteEntity> _entities = new List<SoftDeleteEntity>();
        private readonly List<RegularEntity> _regularEntities = new List<RegularEntity>();

        [Query]
        public IQueryable<SoftDeleteEntity> GetEntities()
        {
            var query = _entities.AsQueryable();
            if (!this.ShouldIncludeDeleted)
            {
                query = query.Where(e => !e.IsDeleted);
            }
            return query;
        }

        [Query]
        public IQueryable<RegularEntity> GetRegularEntities()
        {
            return _regularEntities.AsQueryable();
        }

        [Insert]
        public void InsertEntity(SoftDeleteEntity entity)
        {
            _entities.Add(entity);
        }

        [Update]
        public void UpdateEntity(SoftDeleteEntity entity)
        {
            var original = this.ChangeSet.GetOriginal(entity);
            var index = _entities.FindIndex(e => e.Id == original.Id);
            if (index >= 0)
            {
                _entities[index] = entity;
            }
        }

        [Delete]
        public void DeleteEntity(SoftDeleteEntity entity)
        {
            var original = this.ChangeSet.GetOriginal(entity);
            _entities.RemoveAll(e => e.Id == original.Id);
        }

        [Insert]
        public void InsertRegularEntity(RegularEntity entity)
        {
            _regularEntities.Add(entity);
        }

        [Update]
        public void UpdateRegularEntity(RegularEntity entity)
        {
        }

        [Delete]
        public void DeleteRegularEntity(RegularEntity entity)
        {
            var original = this.ChangeSet.GetOriginal(entity);
            _regularEntities.RemoveAll(e => e.Id == original.Id);
        }
    }

    [EnableClientAccess]
    public class CustomSoftDeleteTestDomainService : SoftDeleteTestDomainService
    {
        protected override bool OnSoftDelete(object entity, SoftDeleteConfiguration softDeleteConfig)
        {
            var softDeleteEntity = entity as SoftDeleteEntity;
            if (softDeleteEntity != null)
            {
                softDeleteEntity.DeleteReason = "CustomReason";
            }
            return base.OnSoftDelete(entity, softDeleteConfig);
        }
    }
}
