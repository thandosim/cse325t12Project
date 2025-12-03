using System;
using System.ComponentModel.DataAnnotations;

namespace t12Project.Models
{
    public class CustomerLocationUpdate
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string CustomerId { get; set; } = default!;   // FK to ApplicationUser or Customers table

        public ApplicationUser? Customer { get; set; }       // Navigation property

        public decimal Latitude { get; set; }                // GPS latitude
        public decimal Longitude { get; set; }               // GPS longitude

        public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;

        public string? Notes { get; set; }                   // Optional metadata
    }
}
