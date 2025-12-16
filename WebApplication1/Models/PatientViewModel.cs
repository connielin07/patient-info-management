namespace WebApplication1.Models
{
    public class PatientViewModel
    {
        public long PatientId { get; set; }
        public string IdNo { get; set; }

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
        public string OtherDischargeStatus { get; set; }
        public string AdmitHospital { get; set; }
        public string TransferHospital { get; set; }
        public string Occupation { get; set; }
        public bool HasMajorInjury { get; set; }
        public bool HasDisability { get; set; }
        public DateTime LastModifiedAt { get; set; }

        // 🌟 新增：AI 公費篩檢建議備註
        public string? AITip { get; set; }

    }
}
