using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenRiaServices.Dapper.Sample
{
    /// <summary>
    /// 示例产品实体
    /// 展示如何使用数据注解配置映射
    /// </summary>
    [Table("Products", Schema = "dbo")]
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProductId { get; set; }

        [Required]
        [StringLength(100)]
        [Column("ProductName")]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }

        public int? CategoryId { get; set; }

        [Timestamp]
        [ConcurrencyCheck]
        public byte[]? RowVersion { get; set; }

        [NotMapped]
        public string DisplayName => $"{ProductId}: {Name}";
    }

    /// <summary>
    /// 示例分类实体
    /// 展示复合主键的使用
    /// </summary>
    [Table("Categories")]
    public class Category
    {
        [Key]
        [Column(Order = 1)]
        public int TenantId { get; set; }

        [Key]
        [Column(Order = 2)]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    /// <summary>
    /// 示例订单实体
    /// 展示自定义 SQL 的使用场景
    /// </summary>
    [Table("Orders")]
    public class Order
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OrderId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? CustomerName { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        public int Status { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
