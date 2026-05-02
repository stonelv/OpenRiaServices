using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using OpenRiaServices;
using OpenRiaServices.Server;

namespace TestDomainServices.Validation
{
    [EnableClientAccess]
    public class DateRangeDomainService : DomainService
    {
        private static readonly List<DateRangeEntity> _entities = new List<DateRangeEntity>();

        public IQueryable<DateRangeEntity> GetDateRangeEntities()
        {
            return _entities.AsQueryable();
        }

        public void InsertDateRangeEntity(DateRangeEntity entity)
        {
            _entities.Add(entity);
        }

        public void UpdateDateRangeEntity(DateRangeEntity entity)
        {
            var existing = _entities.FirstOrDefault(e => e.Id == entity.Id);
            if (existing != null)
            {
                existing.StartDate = entity.StartDate;
                existing.EndDate = entity.EndDate;
                existing.Description = entity.Description;
            }
        }

        public void DeleteDateRangeEntity(DateRangeEntity entity)
        {
            _entities.RemoveAll(e => e.Id == entity.Id);
        }
    }

    [CustomValidation(typeof(DateRangeValidator), "ValidateDateRange")]
    public class DateRangeEntity
    {
        [Key]
        [RoundtripOriginal]
        public int Id { get; set; }

        [Required]
        [RoundtripOriginal]
        public DateTime StartDate { get; set; }

        [Required]
        [RoundtripOriginal]
        public DateTime EndDate { get; set; }

        [RoundtripOriginal]
        [StringLength(100)]
        public string Description { get; set; }
    }

    [CustomValidation(typeof(DateRangeValidator), "ValidateDateRange")]
    public class DateRangeEntityWithNullableDates
    {
        [Key]
        [RoundtripOriginal]
        public int Id { get; set; }

        [RoundtripOriginal]
        public DateTime? StartDate { get; set; }

        [RoundtripOriginal]
        public DateTime? EndDate { get; set; }

        [RoundtripOriginal]
        [StringLength(100)]
        public string Description { get; set; }
    }
}
