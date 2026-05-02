using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenRiaServices.Server;
using OpenRiaServices.Server.Test.Utilities;
using TestDomainServices.Validation;

namespace OpenRiaServices.Tools.Test
{
    [TestClass]
    public class DateRangeValidationTests
    {
        private CultureInfo _originalCulture;
        private CultureInfo _originalUICulture;

        [TestInitialize]
        public void TestInitialize()
        {
            _originalCulture = Thread.CurrentThread.CurrentCulture;
            _originalUICulture = Thread.CurrentThread.CurrentUICulture;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Thread.CurrentThread.CurrentCulture = _originalCulture;
            Thread.CurrentThread.CurrentUICulture = _originalUICulture;
            DateRangeResources.Culture = null;
        }

        [TestMethod]
        [Description("验证 StartDate 早于 EndDate 时验证通过")]
        public void DateRangeValidator_ValidDates_ReturnsSuccess()
        {
            var entity = new DateRangeEntity
            {
                Id = 1,
                StartDate = new DateTime(2024, 1, 1),
                EndDate = new DateTime(2024, 12, 31)
            };

            var validationContext = new ValidationContext(entity);
            var validationResults = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(entity, validationContext, validationResults, true);

            Assert.IsTrue(isValid, "验证应该通过");
            Assert.AreEqual(0, validationResults.Count, "不应该有验证错误");
        }

        [TestMethod]
        [Description("验证 StartDate 晚于 EndDate 时验证失败")]
        public void DateRangeValidator_InvalidDates_ReturnsError()
        {
            var entity = new DateRangeEntity
            {
                Id = 1,
                StartDate = new DateTime(2024, 12, 31),
                EndDate = new DateTime(2024, 1, 1)
            };

            var validationContext = new ValidationContext(entity);
            var validationResults = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(entity, validationContext, validationResults, true);

            Assert.IsFalse(isValid, "验证应该失败");
            Assert.IsTrue(validationResults.Count > 0, "应该有验证错误");

            var dateRangeError = validationResults.Find(r => 
                r.ErrorMessage.Contains("StartDate") || r.ErrorMessage.Contains("EndDate"));
            
            Assert.IsNotNull(dateRangeError, "应该找到日期范围验证错误");
            StringAssert.Contains(dateRangeError.ErrorMessage, "StartDate", "错误消息应该包含 StartDate");
            
            Assert.IsTrue(dateRangeError.MemberNames.Contains("StartDate"), "错误应该包含 StartDate 成员");
            Assert.IsTrue(dateRangeError.MemberNames.Contains("EndDate"), "错误应该包含 EndDate 成员");
        }

        [TestMethod]
        [Description("验证日期相等时验证失败")]
        public void DateRangeValidator_EqualDates_ReturnsError()
        {
            var entity = new DateRangeEntity
            {
                Id = 1,
                StartDate = new DateTime(2024, 6, 15),
                EndDate = new DateTime(2024, 6, 15)
            };

            var validationContext = new ValidationContext(entity);
            var validationResults = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(entity, validationContext, validationResults, true);

            Assert.IsFalse(isValid, "验证应该失败，因为开始日期必须早于结束日期");
        }

        [TestMethod]
        [Description("验证英文错误消息")]
        public void DateRangeResources_EnglishCulture_ReturnsEnglishMessage()
        {
            DateRangeResources.Culture = new CultureInfo("en-US");

            string message = DateRangeResources.StartDateMustBeEarlierThanEndDate;

            Assert.AreEqual("StartDate must be earlier than EndDate.", message);
        }

        [TestMethod]
        [Description("验证中文错误消息")]
        public void DateRangeResources_ChineseCulture_ReturnsChineseMessage()
        {
            DateRangeResources.Culture = new CultureInfo("zh-CN");

            string message = DateRangeResources.StartDateMustBeEarlierThanEndDate;

            Assert.AreEqual("开始日期必须早于结束日期。", message);
        }

        [TestMethod]
        [Description("验证使用英文 UI 文化时验证错误消息为英文")]
        public void DateRangeValidator_EnglishUICulture_UsesEnglishMessage()
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            DateRangeResources.Culture = null;

            var entity = new DateRangeEntity
            {
                Id = 1,
                StartDate = new DateTime(2024, 12, 31),
                EndDate = new DateTime(2024, 1, 1)
            };

            var validationContext = new ValidationContext(entity);
            var validationResults = new List<ValidationResult>();

            Validator.TryValidateObject(entity, validationContext, validationResults, true);

            var dateRangeError = validationResults.Find(r => 
                r.MemberNames.Contains("StartDate") && r.MemberNames.Contains("EndDate"));
            
            Assert.IsNotNull(dateRangeError);
            StringAssert.Contains(dateRangeError.ErrorMessage, "StartDate must be earlier");
        }

        [TestMethod]
        [Description("验证使用中文 UI 文化时验证错误消息为中文")]
        public void DateRangeValidator_ChineseUICulture_UsesChineseMessage()
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-CN");
            DateRangeResources.Culture = null;

            var entity = new DateRangeEntity
            {
                Id = 1,
                StartDate = new DateTime(2024, 12, 31),
                EndDate = new DateTime(2024, 1, 1)
            };

            var validationContext = new ValidationContext(entity);
            var validationResults = new List<ValidationResult>();

            Validator.TryValidateObject(entity, validationContext, validationResults, true);

            var dateRangeError = validationResults.Find(r => 
                r.MemberNames.Contains("StartDate") && r.MemberNames.Contains("EndDate"));
            
            Assert.IsNotNull(dateRangeError);
            StringAssert.Contains(dateRangeError.ErrorMessage, "开始日期必须早于结束日期");
        }

        [TestMethod]
        [Description("验证可空日期在两个日期都有值时进行验证")]
        public void DateRangeValidator_NullableDates_WithValues_Validates()
        {
            var entity = new DateRangeEntityWithNullableDates
            {
                Id = 1,
                StartDate = new DateTime(2024, 12, 31),
                EndDate = new DateTime(2024, 1, 1)
            };

            var validationContext = new ValidationContext(entity);
            var validationResults = new List<ValidationResult>();

            bool isValid = Validator.TryValidateObject(entity, validationContext, validationResults, true);

            Assert.IsFalse(isValid, "验证应该失败");
        }

        [TestMethod]
        [Description("验证可空日期在任一日期为空时跳过验证")]
        public void DateRangeValidator_NullableDates_WithNull_SkipsValidation()
        {
            var entity1 = new DateRangeEntityWithNullableDates
            {
                Id = 1,
                StartDate = null,
                EndDate = new DateTime(2024, 1, 1)
            };

            var entity2 = new DateRangeEntityWithNullableDates
            {
                Id = 1,
                StartDate = new DateTime(2024, 1, 1),
                EndDate = null
            };

            var validationContext1 = new ValidationContext(entity1);
            var validationResults1 = new List<ValidationResult>();
            bool isValid1 = Validator.TryValidateObject(entity1, validationContext1, validationResults1, true);

            var validationContext2 = new ValidationContext(entity2);
            var validationResults2 = new List<ValidationResult>();
            bool isValid2 = Validator.TryValidateObject(entity2, validationContext2, validationResults2, true);

            var dateRangeError1 = validationResults1.Find(r => 
                r.MemberNames.Contains("StartDate") && r.MemberNames.Contains("EndDate"));
            var dateRangeError2 = validationResults2.Find(r => 
                r.MemberNames.Contains("StartDate") && r.MemberNames.Contains("EndDate"));

            Assert.IsNull(dateRangeError1, "当 StartDate 为空时不应该触发日期范围验证");
            Assert.IsNull(dateRangeError2, "当 EndDate 为空时不应该触发日期范围验证");
        }

        [TestMethod]
        [Description("验证资源类的所有属性")]
        public void DateRangeResources_AllProperties_ReturnCorrectValues()
        {
            DateRangeResources.Culture = new CultureInfo("en-US");
            
            Assert.AreEqual("StartDate is required.", DateRangeResources.StartDateRequired);
            Assert.AreEqual("EndDate is required.", DateRangeResources.EndDateRequired);
            Assert.AreEqual("StartDate must be earlier than EndDate.", DateRangeResources.StartDateMustBeEarlierThanEndDate);

            DateRangeResources.Culture = new CultureInfo("zh-CN");
            
            Assert.AreEqual("开始日期是必填项。", DateRangeResources.StartDateRequired);
            Assert.AreEqual("结束日期是必填项。", DateRangeResources.EndDateRequired);
            Assert.AreEqual("开始日期必须早于结束日期。", DateRangeResources.StartDateMustBeEarlierThanEndDate);
        }

        [TestMethod]
        [Description("验证文化设置可以独立于线程文化")]
        public void DateRangeResources_CultureProperty_OverridesThreadCulture()
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            
            DateRangeResources.Culture = new CultureInfo("zh-CN");
            Assert.AreEqual("开始日期必须早于结束日期。", DateRangeResources.StartDateMustBeEarlierThanEndDate);

            DateRangeResources.Culture = new CultureInfo("en-US");
            Assert.AreEqual("StartDate must be earlier than EndDate.", DateRangeResources.StartDateMustBeEarlierThanEndDate);

            DateRangeResources.Culture = null;
            Assert.AreEqual("StartDate must be earlier than EndDate.", DateRangeResources.StartDateMustBeEarlierThanEndDate);
        }
    }
}
