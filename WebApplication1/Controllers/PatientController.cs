using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Data.SqlClient;
using System.Data;
using WebApplication1.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace WebApplication1.Controllers
{
    public class PatientController : Controller
    {
        private readonly string ConnStr;

        public PatientController(IConfiguration configuration)
        {
            ConnStr = configuration.GetConnectionString("PatientDatabase") ?? throw new InvalidOperationException("Connection string 'PatientDatabase' not found.");
        }

        public IActionResult Index()
        {
            var patientViewModel = new PatientViewModel();
            var genderCodeList = new List<SelectListItem>
            {
                new SelectListItem { Text = "男", Value = "M" },
                new SelectListItem { Text = "女", Value = "F" }
            };

            ViewBag.PatientViewModel = patientViewModel;
            ViewBag.GenderCodeList = genderCodeList;

            return View();
        }

        [HttpPost]
        public IActionResult Save([FromForm] PatientViewModel patientViewModel) 
        {
            try
            {
                var patientDBModel = ConvertPatientViewModeltoDBModel(patientViewModel);

                var dbResult = false;

                if (patientDBModel.PatientId == 0)
                {
                    dbResult = InsertPatient(patientDBModel).Result > 0;
                }
                else
                {
                    dbResult = UpdatePatient(patientDBModel).Result;
                }

                if (dbResult)
                {
                    return Ok(dbResult);
                }

                return BadRequest(dbResult);
            }
            catch (Exception ex)
            {
                return Json(new { Status = "Error", Error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Search(long? patientId, string? idNo, string? familyName, string? givenName)
        {
            try
            {
                var resultList = new List<PatientViewModel>();
                var dbResult = QueryPatientList(patientId, idNo, familyName, givenName).Result;
                if (dbResult.Count() >= 0)
                {
                    resultList = dbResult.Select(ConvertPatientDBModeltoViewModel).ToList();
                    return Ok(resultList);
                }

                return BadRequest("查詢失敗");
            }
            catch (Exception ex)
            {
                return Json(new { Status = "Error", Error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Delete(long patientId)
        {
            try
            {
                var patientDBModel = new PatientDBModel();

                // 依照 PatientId 查詢該筆資料
                var patientDBModelList = QueryPatientList(patientId).Result;

                if (patientDBModelList.Count() > 0)
                {
                    patientDBModel = patientDBModelList.First();
                }

                // 將該筆資料設定為【未啟用】
                patientDBModel.Active = false;

                var dbResult = UpdatePatient(patientDBModel);

                if (dbResult.Result)
                {
                    return Ok(dbResult.Result);
                }

                return BadRequest(dbResult);
            }
            catch (Exception ex)
            {
                return Json(new { Status = "Error", Error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ExportFHIR(long patientId)
        {
            // 用你原本的查詢方法抓資料
            var patientList = QueryPatientList(patientId).Result;

            if (patientList == null || patientList.Count == 0)
            {
                return NotFound("查無此病人");
            }

            var p = patientList.First();

            // 這裡做一個「簡化版」FHIR Patient 資源
            var fhirPatient = new
            {
                resourceType = "Patient",
                id = p.PatientId.ToString(),
                active = p.Active,
                identifier = new[]
                {
            new
            {
                use = "official",
                system = "http://example.org/hospital/patient/idno",
                value = p.IdNo
            }
        },
                name = new[]
                {
            new
            {
                use = "official",
                family = p.FamilyName,
                given = new[] { p.GivenName }
            }
        },
                telecom = string.IsNullOrWhiteSpace(p.Telecom)
                    ? null
                    : new[]
                    {
                new
                {
                    system = "phone",
                    value = p.Telecom,
                    use = "mobile"
                }
                    },
                gender = p.Gender == "M" ? "male" : "female",
                birthDate = p.Birthday.ToString("yyyy-MM-dd"),
                address = string.IsNullOrWhiteSpace(p.Address)
                    ? null
                    : new[]
                    {
                new
                {
                    text = p.Address
                }
                    },
                // 下面這些其實比較像 Encounter 的欄位，這裡先用 extension 放進去
                extension = new[]
{
    new
    {
        url = "http://example.org/fhir/StructureDefinition/admitDate",
        valueString = p.AdmitDate.ToString("yyyy-MM-dd")
    },
    new
    {
        url = "http://example.org/fhir/StructureDefinition/dischargeDate",
        valueString = (p.DischargeDate == DateTime.MinValue
            ? ""
            : p.DischargeDate.ToString("yyyy-MM-dd"))
    },
    new
    {
        url = "http://example.org/fhir/StructureDefinition/dischargeStatus",
        valueString = p.DischargeStatus
    },
    new
    {
        url = "http://example.org/fhir/StructureDefinition/transferHospital",
        valueString = p.TransferHospital
    }
}
            };

            var json = JsonSerializer.Serialize(
                fhirPatient,
                new JsonSerializerOptions { WriteIndented = true }
            );

            var bytes = Encoding.UTF8.GetBytes(json);
            var fileName = $"Patient-{p.PatientId}.json";

            // Content-Type 用 application/fhir+json
            return File(bytes, "application/fhir+json", fileName);
        }

        #region SQL
        public Task<long> InsertPatient(PatientDBModel patient)
        {
            long insertId = 0;

            SqlConnection connection = new SqlConnection(ConnStr);

            var insertStr = @"INSERT INTO DB1.dbo.Patient
                (IdNo, Active, FamilyName, GivenName, Telecom, Gender, Birthday, Address, AdmitDate, DischargeDate, DischargeStatus, TransferHospital)
                VALUES (@IdNo, @Active, @FamilyName, @GivenName, @Telecom, @Gender, @Birthday, @Address, @AdmitDate, @DischargeDate, @DischargeStatus, @TransferHospital)
                SELECT @InsertId = SCOPE_IDENTITY()";

            SqlCommand command = new SqlCommand(insertStr, connection);

            SqlParameter outPutValue = new SqlParameter("@InsertId", SqlDbType.BigInt);
            outPutValue.Direction = ParameterDirection.Output;

            command.Parameters.Add(outPutValue);
            command.Parameters.Add(new SqlParameter("@IdNo", patient.IdNo));
            command.Parameters.Add(new SqlParameter("@Active", patient.Active));
            command.Parameters.Add(new SqlParameter("@FamilyName", patient.FamilyName));
            command.Parameters.Add(new SqlParameter("@GivenName", patient.GivenName));
            command.Parameters.Add(new SqlParameter("@Telecom", patient.Telecom));
            command.Parameters.Add(new SqlParameter("@Gender", patient.Gender));
            command.Parameters.Add(new SqlParameter("@Birthday", patient.Birthday.ToString("yyyy/MM/dd")));
            command.Parameters.Add(new SqlParameter("@Address", patient.Address));
            command.Parameters.Add(new SqlParameter("@AdmitDate", patient.AdmitDate.ToString("yyyy/MM/dd")));
            command.Parameters.Add(new SqlParameter("@DischargeDate", patient.DischargeDate.ToString("yyyy/MM/dd")));
            command.Parameters.Add(new SqlParameter("@DischargeStatus", patient.DischargeStatus));
            command.Parameters.Add(new SqlParameter("@TransferHospital", string.IsNullOrWhiteSpace(patient.TransferHospital) ? DBNull.Value : patient.TransferHospital));

            connection.Open();
            command.ExecuteNonQuery();
            connection.Close();

            if(outPutValue.Value != DBNull.Value)
            {
                insertId = Convert.ToInt64(outPutValue.Value);
            }

            return Task.FromResult(insertId);
        }

        public Task<List<PatientDBModel>> QueryPatientList(long? patientId = null, string? idNo = null, string? familyName = null, string? givenName = null, string? dischargeStatus = null, string? transferHospital = null)
        {
            var result = new List<PatientDBModel>();
            SqlConnection connection = new SqlConnection(ConnStr);
            var param = new List<SqlParameter>();
            var queryStr = @"SELECT PatientId
                                    , IdNo
                                    , Active
                                    , FamilyName
                                    , GivenName
                                    , Telecom
                                    , Gender
                                    , Birthday
                                    , Address
                                    , AdmitDate
                                    , DischargeDate
                                    , DischargeStatus
                                    , TransferHospital
                            FROM DB1.dbo.Patient
                            WHERE 1=1 ";

            queryStr += " AND Active = @Active";
            param.Add(new SqlParameter("@Active", true));

            if (patientId != null)
            {
                queryStr += " AND PatientId = @PatientId ";
                param.Add(new SqlParameter("@PatientId", patientId));
            }

            if (idNo != null)
            {
                queryStr += " AND IdNo = @IdNo ";
                param.Add(new SqlParameter("@IdNo", idNo));
            }

            if (familyName != null)
            {
                queryStr += " AND FamilyName LIKE '%' + @FamilyName + '%'";
                param.Add(new SqlParameter("@FamilyName", familyName));
            }

            if (givenName != null)
            {
                queryStr += " AND GivenName LIKE '%' + @GivenName + '%'";
                param.Add(new SqlParameter("@GivenName", givenName));
            }

            if (dischargeStatus != null)
            {
                queryStr += " AND DischargeStatus = @DischargeStatus ";
                param.Add(new SqlParameter("@DischargeStatus", dischargeStatus));
            }

            if (transferHospital != null)
            {
                queryStr += " AND TransferHospital = @TransferHospital ";
                param.Add(new SqlParameter("@TransferHospital", transferHospital));
            }

            SqlCommand command = new SqlCommand(queryStr, connection);

            foreach (var p in param)
            {
                command.Parameters.Add(p);
            }

            connection.Open();
            SqlDataReader reader = command.ExecuteReader();

            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    var patient = new PatientDBModel
                    {
                        PatientId = reader.GetInt64(reader.GetOrdinal("PatientId")),
                        IdNo = reader.GetString(reader.GetOrdinal("IdNo")),
                        Active = reader.GetBoolean(reader.GetOrdinal("Active")),
                        FamilyName = reader.GetString(reader.GetOrdinal("FamilyName")),
                        GivenName = reader.GetString(reader.GetOrdinal("GivenName")),
                        Telecom = reader.IsDBNull(reader.GetOrdinal("Telecom")) ? string.Empty : reader.GetString(reader.GetOrdinal("Telecom")),
                        Gender = reader.GetString(reader.GetOrdinal("Gender")),
                        Birthday = reader.GetDateTime(reader.GetOrdinal("Birthday")),
                        Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? string.Empty : reader.GetString(reader.GetOrdinal("Address")),
                        AdmitDate = reader.GetDateTime(reader.GetOrdinal("AdmitDate")),
                        DischargeDate = reader.IsDBNull(reader.GetOrdinal("DischargeDate")) ? DateTime.MinValue : reader.GetDateTime(reader.GetOrdinal("DischargeDate")),
                        DischargeStatus = reader.IsDBNull(reader.GetOrdinal("DischargeStatus")) ? string.Empty : reader.GetString(reader.GetOrdinal("DischargeStatus")),
                        TransferHospital = reader.IsDBNull(reader.GetOrdinal("TransferHospital")) ? string.Empty : reader.GetString(reader.GetOrdinal("TransferHospital")),
                    };

                    result.Add(patient);
                }
            }
            else
            {
                Console.WriteLine("No data found");
            }

            connection.Close();

            return Task.FromResult(result);
        }
        
        public Task<bool> UpdatePatient(PatientDBModel patient)
        {
            bool result = false;
            SqlConnection connection = new SqlConnection(ConnStr);
            var inserteStr = @"UPDATE DB1.dbo.Patient
                               SET IdNo = @IdNo,
                                   Active = @Active,
                                   FamilyName = @FamilyName,
                                   GivenName = @GivenName,
                                   Telecom = @Telecom,
                                   Gender = @Gender,
                                   Birthday = @Birthday,
                                   Address = @Address,
                                   AdmitDate = @AdmitDate,
                                   DischargeDate = @DischargeDate,
                                   DischargeStatus = @DischargeStatus,
                                   TransferHospital = @TransferHospital
                               WHERE PatientId = @PatientId";

            SqlCommand command = new SqlCommand(inserteStr, connection);

            command.Parameters.Add(new SqlParameter("@PatientId", patient.PatientId));
            command.Parameters.Add(new SqlParameter("@IdNo", patient.IdNo));
            command.Parameters.Add(new SqlParameter("@Active", patient.Active));
            command.Parameters.Add(new SqlParameter("@FamilyName", patient.FamilyName));
            command.Parameters.Add(new SqlParameter("@GivenName", patient.GivenName));
            command.Parameters.Add(new SqlParameter("@Telecom", patient.Telecom));
            command.Parameters.Add(new SqlParameter("@Gender", patient.Gender));
            command.Parameters.Add(new SqlParameter("@Birthday", patient.Birthday.ToString("yyyy/MM/dd")));
            command.Parameters.Add(new SqlParameter("@Address", patient.Address));
            command.Parameters.Add(new SqlParameter("@AdmitDate", patient.AdmitDate.ToString("yyyy/MM/dd")));
            command.Parameters.Add(new SqlParameter("@DischargeDate", patient.DischargeDate.ToString("yyyy/MM/dd")));
            command.Parameters.Add(new SqlParameter("@DischargeStatus", patient.DischargeStatus));
            command.Parameters.Add(new SqlParameter("@TransferHospital", string.IsNullOrWhiteSpace(patient.TransferHospital) ? DBNull.Value : patient.TransferHospital));

            connection.Open();
            var updateResult = command.ExecuteNonQuery();
            connection.Close();

            if (updateResult > 0)
            {
                result = true;
            }
            return Task.FromResult(result);
        }

        #endregion SQL

        #region Private

        private PatientDBModel ConvertPatientViewModeltoDBModel(PatientViewModel viewModel)
        {
            return new PatientDBModel()
            {
                PatientId = viewModel.PatientId,
                IdNo = viewModel.IdNo,
                Active = viewModel.Active,
                FamilyName = viewModel.FamilyName,
                GivenName = viewModel.GivenName,
                Telecom = viewModel.Telecom,
                Gender = viewModel.Gender,
                Birthday = viewModel.Birthday,
                Address = viewModel.Address,
                AdmitDate = viewModel.AdmitDate,
                DischargeDate = viewModel.DischargeDate,
                DischargeStatus = viewModel.DischargeStatus,
                TransferHospital = viewModel.TransferHospital,
            };
        }

        private PatientViewModel ConvertPatientDBModeltoViewModel(PatientDBModel dbModel)
        {
            return new PatientViewModel()
            {
                PatientId = dbModel.PatientId,
                IdNo = dbModel.IdNo,
                Active = dbModel.Active,
                FamilyName = dbModel.FamilyName,
                GivenName = dbModel.GivenName,
                Telecom = dbModel.Telecom,
                Gender = dbModel.Gender,
                Birthday = dbModel.Birthday,
                Address = dbModel.Address,
                AdmitDate = dbModel.AdmitDate,
                DischargeDate = dbModel.DischargeDate,
                DischargeStatus = dbModel.DischargeStatus,
                TransferHospital = dbModel.TransferHospital,
            };
        }

        #endregion Private
    }
}
