using System.ComponentModel.DataAnnotations;

namespace NhaNghiYenNhi.Models
{
    public class MonAn
    {
        public int Id { get; set; }
        
        [Required]
        public string TenMon { get; set; }
        
        [Required]
        public decimal Gia { get; set; }
        
        public string MoTa { get; set; }
        
        public string HinhAnh { get; set; }
        
        public bool TrangThai { get; set; } = true;
    }
} 