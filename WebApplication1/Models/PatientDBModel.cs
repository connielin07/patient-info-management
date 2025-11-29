namespace WebApplication1.Models
{
    public class PatientDBModel
    {
        public long PatientId { get; set; }
        public string IdNo { get; set; }
        public bool Active { get; set; }
        public string FamilyName { get; set; }
        public string GivenName { get; set; }
        public string Telecom { get; set; }
        public string Gender { get; set; }
        public DateTime Birthday { get; set; }
        public string Address { get; set; }
        public string IsHospitalized { get; set; } // Y/N

        public DateTime? AdmitDate { get; set; }
        public DateTime? DischargeDate { get; set; }
        public string DischargeStatus { get; set; }
        public string TransferHospital { get; set; }
    }
}
