﻿namespace BOM_API_v2.KaizenFiles.Models
{
    public class Sales
    {
        public string Name { get; set; } // order name
        public int Number { get; set; } // contact number
        public string Email { get; set; }
        public double Cost { get; set; }
        public int Total { get; set; }
        public DateTime Date { get; set; }
    }

}