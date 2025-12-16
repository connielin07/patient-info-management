using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using System.Text.Json;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class PatientController : Controller
    {
        private readonly string ConnStr;
        private string admitDateString;
        private string dischargeDateString;
        private readonly Kernel _kernel;


        // 更新建構函式，注入 IConfiguration 和 Kernel
        public PatientController(IConfiguration configuration, Kernel kernel)
        {
            ConnStr = configuration.GetConnectionString("PatientDatabase") ?? throw new InvalidOperationException("Connection string 'PatientDatabase' not found.");
            _kernel = kernel; // 👈 賦值
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
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                string errorMessage = "身分證字號重複：該身分證字號已經存在於資料庫中。";
                return Content(errorMessage, "text/plain"); 
            }
            catch (Exception ex)
            {
                return Json(new { Status = "Error", Error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Search(
            long? patientId,
            string? idNo,
            string? familyName,
            string? givenName,
            string? dischargeStatus,
            string? transferHospital,
            string? occupation,
            bool? hasMajorInjury,
            bool? hasDisability,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 5, 100);

                if (patientId.HasValue && patientId.Value <= 0)
                {
                    patientId = null;
                }

                idNo = string.IsNullOrWhiteSpace(idNo) ? null : idNo;
                familyName = string.IsNullOrWhiteSpace(familyName) ? null : familyName;
                givenName = string.IsNullOrWhiteSpace(givenName) ? null : givenName;
                dischargeStatus = string.IsNullOrWhiteSpace(dischargeStatus) ? null : dischargeStatus;
                transferHospital = string.IsNullOrWhiteSpace(transferHospital) ? null : transferHospital;
                occupation = string.IsNullOrWhiteSpace(occupation) ? null : occupation;

                var pagedResult = QueryPatientListPaged(patientId, idNo, familyName, givenName, dischargeStatus, transferHospital, occupation, hasMajorInjury, hasDisability, page, pageSize).Result;
                var resultList = pagedResult.Items.Select(ConvertPatientDBModeltoViewModel).ToList();

                var response = new PagedResult<PatientViewModel>
                {
                    Items = resultList,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = pagedResult.TotalCount
                };

                return Ok(response);
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
            var patientList = QueryPatientList(patientId).Result;

            if (patientList == null || patientList.Count == 0)
            {
                return NotFound("查無此病人");
            }

            var p = patientList.First();

            // 這裡要確保 DateTime? 真的會是 null，而不是 MinValue（下面會改 QueryPatientList）
            var admitDateString = p.AdmitDate.HasValue ? p.AdmitDate.Value.ToString("yyyy-MM-dd") : null;
            var dischargeDateString = p.DischargeDate.HasValue ? p.DischargeDate.Value.ToString("yyyy-MM-dd") : null;

            // 先用 List<object> 動態組 extension，避免空值也被輸出
            var extensions = new List<object>();

            if (!string.IsNullOrWhiteSpace(p.IsHospitalized))
            {
                extensions.Add(new
                {
                    url = "https://your-hospital.tw/fhir/StructureDefinition/patient-isHospitalized", // TODO: 換成你自己的 URL
                    valueString = p.IsHospitalized
                });
            }

            if (!string.IsNullOrWhiteSpace(admitDateString))
            {
                extensions.Add(new
                {
                    url = "https://your-hospital.tw/fhir/StructureDefinition/patient-admitDate",     // TODO
                    valueDateTime = admitDateString                                                // 用 dateTime 比 valueString 更合理
                });
            }

            if (!string.IsNullOrWhiteSpace(dischargeDateString))
            {
                extensions.Add(new
                {
                    url = "https://your-hospital.tw/fhir/StructureDefinition/patient-dischargeDate", // TODO
                    valueDateTime = dischargeDateString
                });
            }

            if (!string.IsNullOrWhiteSpace(p.DischargeStatus))
            {
                extensions.Add(new
                {
                    url = "https://your-hospital.tw/fhir/StructureDefinition/patient-dischargeStatus", // TODO
                    valueString = p.DischargeStatus
                });
            }

            if (!string.IsNullOrWhiteSpace(p.OtherDischargeStatus))
            {
                extensions.Add(new
                {
                    url = "https://your-hospital.tw/fhir/StructureDefinition/patient-otherDischargeStatus", // TODO
                    valueString = p.OtherDischargeStatus
                });
            }

            // 🔴 重點：只有在有值的時候才加 transferHospital 這個 extension
            if (!string.IsNullOrWhiteSpace(p.TransferHospital))
            {
                extensions.Add(new
                {
                    url = "https://your-hospital.tw/fhir/StructureDefinition/patient-transferHospital", // TODO
                    valueString = p.TransferHospital
                });
            }

            if (!string.IsNullOrWhiteSpace(p.AdmitHospital))
            {
                extensions.Add(new
                {
                    url = "https://your-hospital.tw/fhir/StructureDefinition/patient-admitHospital", // TODO
                    valueString = p.AdmitHospital
                });
            }

            var fhirPatient = new
            {
                resourceType = "Patient",
                id = p.PatientId.ToString(),
                active = p.Active,

                // （可選）加 narrative，解掉 dom-6 警告
                text = new
                {
                    status = "generated",
                    div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\">病人：{p.FamilyName}{p.GivenName}（ID：{p.IdNo}）</div>"
                },

                identifier = new[]
                {
                    new
                    {
                        use = "official",
                        // 身分證字號 namespace，TW Core Patient-DS 建議使用 http://www.moi.gov.tw
                        system = "http://www.moi.gov.tw",
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

                // 如果沒有任何 extension，就不要輸出這個欄位（避免出現 extension: []）
                extension = extensions.Count > 0 ? extensions : null
            };

            var json = JsonSerializer.Serialize(
                fhirPatient,
                new JsonSerializerOptions { WriteIndented = true }
            );

            var bytes = Encoding.UTF8.GetBytes(json);
            var fileName = $"Patient-{p.PatientId}.json";

            return File(bytes, "application/fhir+json", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> GenerateAITip()
        {
            // 1. 定義給 AI 的提示 (Prompt)
            var promptTemplate = @"
                請以輕鬆幽默或關懷的語氣，為一位長時間專注在病人資訊清單上的醫生，寫一句簡短的休息或喝水提醒。
                只需提供提醒內容，不要任何多餘的開頭或結尾語，例如 '提醒：' 或 '請注意：'。
                你的回答必須簡潔，不超過一句話。
            ";

            try
            {
                // 2. 使用 Semantic Kernel 呼叫 OpenAI API
                var result = await _kernel.InvokePromptAsync(
                    promptTemplate,
                    new(new OpenAIPromptExecutionSettings()
                    {
                        Temperature = 0.8, // 提高隨機性
                        MaxTokens = 50     // 限制長度
                    })
                );

                var tip = result.ToString().Trim();

                if (!string.IsNullOrWhiteSpace(tip))
                {
                    // 成功時回傳 JSON，包含 AI 生成的提示
                    return Ok(new { Tip = tip });
                }
                else
                {
                    // API 呼叫成功但沒有內容
                    return Ok(new { Tip = "休息是最好的醫療處方，請稍作休息吧。" });
                }
            }
            catch (Exception ex)
            {
                // 捕捉網路或服務錯誤，返回預設的提示
                System.Diagnostics.Debug.WriteLine($"Semantic Kernel Error: {ex.Message}");
                return Ok(new { Tip = "系統忙碌中，建議您閉眼休息 30 秒。" });
            }
        }

        // 🌟 AI 公費篩檢建議 Action
        [HttpPost]
        public async Task<IActionResult> GenerateScreeningTip([FromForm] PatientViewModel patient)
        {
            if (patient == null)
            {
                return BadRequest("病人資料不得為空。");
            }

            // 組合輸入給 AI 的 Prompt
            // 這裡使用中文，並帶入病人的關鍵資訊
            var promptTemplate = $@"
                客觀不偏袒的以衛教專員角度，根據以下病人資訊，只以列點方式簡短建議適合的「公費」癌症篩檢或健康檢查項目，不加前綴或後綴字
                如：「大腸癌篩檢」、「B、C型肝炎篩檢」
                若無適合項目，回答「目前沒有特別建議」

                年齡：{DateTime.Now.Year - patient.Birthday.Year} 歲 (生日：{patient.Birthday.ToString("yyyy-MM-dd")})
                性別：{(patient.Gender == "M" ? "男" : "女")}
                職業：{patient.Occupation ?? "N/A"}
                重大傷病：{(patient.HasMajorInjury ? "是" : "否")}
                身心障礙：{(patient.HasDisability ? "是" : "否")}
                是否住院：{(patient.IsHospitalized == "Y" ? "是" : "否")}
            ";

            try
            {
                // 使用 Semantic Kernel 呼叫 OpenAI API
                var result = await _kernel.InvokePromptAsync(
                    promptTemplate,
                    new(new OpenAIPromptExecutionSettings()
                    {
                        Temperature = 0.5, // 保持一定程度的準確性
                        MaxTokens = 150   // 限制長度
                    })
                );

                var tip = result.ToString().Trim();

                if (!string.IsNullOrWhiteSpace(tip))
                {
                    // 成功時回傳 JSON，包含 AI 生成的提示
                    return Ok(new { Status = "Success", Tip = tip });
                }
                else
                {
                    // AI 未回傳內容時，給一個預設禮貌回答
                    return Ok(new { Status = "Error", Tip = "目前沒有特別建議。" });
                }
            }
            catch (Exception ex)
            {
                // 捕捉網路或服務錯誤
                System.Diagnostics.Debug.WriteLine($"AI Screening Tip Error: {ex.Message}");
                return StatusCode(500, new { Status = "Error", Error = "AI 服務連線失敗，請稍後再試。" });
            }
        }

        #region SQL
        public Task<long> InsertPatient(PatientDBModel patient)
        {
            long insertId = 0;
            using var connection = new SqlConnection(ConnStr);

            var insertStr = @"
                INSERT INTO DB1.dbo.Patient
                (
                    IdNo, Active, FamilyName, GivenName, Telecom, Gender, Birthday,
                    Address, IsHospitalized, AdmitDate, DischargeDate, DischargeStatus,
                    TransferHospital, OtherDischargeStatus, AdmitHospital, Occupation,
                    HasMajorInjury, HasDisability, LastModifiedAt
                )
                VALUES
                (
                    @IdNo, @Active, @FamilyName, @GivenName, @Telecom, @Gender, @Birthday,
                    @Address, @IsHospitalized, @AdmitDate, @DischargeDate, @DischargeStatus,
                    @TransferHospital, @OtherDischargeStatus, @AdmitHospital, @Occupation,
                    @HasMajorInjury, @HasDisability, SYSDATETIME()
                );

                SELECT @InsertId = SCOPE_IDENTITY();";

            using var command = new SqlCommand(insertStr, connection);

            var outPutValue = new SqlParameter("@InsertId", SqlDbType.BigInt)
            {
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outPutValue);

            command.Parameters.Add(new SqlParameter("@IdNo", patient.IdNo));
            command.Parameters.Add(new SqlParameter("@Active", patient.Active));
            command.Parameters.Add(new SqlParameter("@FamilyName", patient.FamilyName));
            command.Parameters.Add(new SqlParameter("@GivenName", patient.GivenName));
            command.Parameters.Add(new SqlParameter("@Telecom", patient.Telecom));
            command.Parameters.Add(new SqlParameter("@Gender", patient.Gender));
            command.Parameters.Add(new SqlParameter("@Birthday", patient.Birthday.ToString("yyyy/MM/dd")));
            command.Parameters.Add(new SqlParameter("@Address", patient.Address));
            command.Parameters.Add(new SqlParameter("@IsHospitalized", patient.IsHospitalized));
            command.Parameters.Add(new SqlParameter("@AdmitDate", patient.AdmitDate.HasValue ? patient.AdmitDate.Value.ToString("yyyy/MM/dd") : DBNull.Value));
            command.Parameters.Add(new SqlParameter("@DischargeDate", patient.DischargeDate.HasValue ? patient.DischargeDate.Value.ToString("yyyy/MM/dd") : DBNull.Value));
            command.Parameters.Add(new SqlParameter("@DischargeStatus", string.IsNullOrWhiteSpace(patient.DischargeStatus) ? DBNull.Value : patient.DischargeStatus));
            command.Parameters.Add(new SqlParameter("@TransferHospital", string.IsNullOrWhiteSpace(patient.TransferHospital) ? DBNull.Value : patient.TransferHospital));
            command.Parameters.Add(new SqlParameter("@OtherDischargeStatus", string.IsNullOrWhiteSpace(patient.OtherDischargeStatus) ? DBNull.Value : patient.OtherDischargeStatus));
            command.Parameters.Add(new SqlParameter("@AdmitHospital", string.IsNullOrWhiteSpace(patient.AdmitHospital) ? DBNull.Value : patient.AdmitHospital));
            command.Parameters.Add(new SqlParameter("@Occupation", string.IsNullOrWhiteSpace(patient.Occupation) ? DBNull.Value : patient.Occupation));
            command.Parameters.Add(new SqlParameter("@HasMajorInjury", patient.HasMajorInjury));
            command.Parameters.Add(new SqlParameter("@HasDisability", patient.HasDisability));

            connection.Open();
            command.ExecuteNonQuery();
            connection.Close();

            if (outPutValue.Value != DBNull.Value)
            {
                insertId = Convert.ToInt64(outPutValue.Value);
            }

            return Task.FromResult(insertId);
        }

        public Task<(List<PatientDBModel> Items, int TotalCount)> QueryPatientListPaged(long? patientId = null, string? idNo = null, string? familyName = null, string? givenName = null, string? dischargeStatus = null, string? transferHospital = null, string? occupation = null, bool? hasMajorInjury = null, bool? hasDisability = null, int page = 1, int pageSize = 20)
        {
            var result = new List<PatientDBModel>();
            var totalCount = 0;
            var connection = new SqlConnection(ConnStr);
            var param = new List<SqlParameter>();
            var baseQuery = @"FROM DB1.dbo.Patient
                            WHERE 1=1 ";

            baseQuery += " AND Active = @Active";
            param.Add(new SqlParameter("@Active", true));

            if (patientId != null)
            {
                baseQuery += " AND PatientId = @PatientId ";
                param.Add(new SqlParameter("@PatientId", patientId));
            }

            if (!string.IsNullOrWhiteSpace(idNo))
            {
                baseQuery += " AND IdNo = @IdNo ";
                param.Add(new SqlParameter("@IdNo", idNo));
            }

            if (!string.IsNullOrWhiteSpace(familyName))
            {
                baseQuery += " AND FamilyName LIKE '%' + @FamilyName + '%'";
                param.Add(new SqlParameter("@FamilyName", familyName));
            }

            if (!string.IsNullOrWhiteSpace(givenName))
            {
                baseQuery += " AND GivenName LIKE '%' + @GivenName + '%'";
                param.Add(new SqlParameter("@GivenName", givenName));
            }

            if (!string.IsNullOrWhiteSpace(occupation))
            {
                baseQuery += " AND Occupation LIKE '%' + @Occupation + '%'";
                param.Add(new SqlParameter("@Occupation", occupation));
            }

            if (hasMajorInjury.HasValue)
            {
                baseQuery += " AND HasMajorInjury = @HasMajorInjury ";
                param.Add(new SqlParameter("@HasMajorInjury", hasMajorInjury.Value));
            }

            if (hasDisability.HasValue)
            {
                baseQuery += " AND HasDisability = @HasDisability ";
                param.Add(new SqlParameter("@HasDisability", hasDisability.Value));
            }

            if (!string.IsNullOrWhiteSpace(dischargeStatus))
            {
                baseQuery += " AND DischargeStatus = @DischargeStatus ";
                param.Add(new SqlParameter("@DischargeStatus", dischargeStatus));
            }

            if (!string.IsNullOrWhiteSpace(transferHospital))
            {
                baseQuery += " AND TransferHospital = @TransferHospital ";
                param.Add(new SqlParameter("@TransferHospital", transferHospital));
            }

            var countSql = $"SELECT COUNT(1) {baseQuery}";
            var queryStr = @$"SELECT PatientId
                                    , IdNo
                                    , Active
                                    , FamilyName
                                    , GivenName
                                    , Telecom
                                    , Gender
                                    , Birthday
                                    , Address
                                    , IsHospitalized
                                    , AdmitDate
                                    , DischargeDate
                                    , DischargeStatus
                                    , TransferHospital
                                    , OtherDischargeStatus 
                                    , AdmitHospital 
                                    , Occupation
                                    , HasMajorInjury
                                    , HasDisability
                                    , LastModifiedAt
                            {baseQuery}
                            ORDER BY PatientId
                            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var countCommand = new SqlCommand(countSql, connection);
            var dataCommand = new SqlCommand(queryStr, connection);

            foreach (var p in param)
            {
                countCommand.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
                dataCommand.Parameters.Add(new SqlParameter(p.ParameterName, p.Value));
            }

            dataCommand.Parameters.Add(new SqlParameter("@Offset", (page - 1) * pageSize));
            dataCommand.Parameters.Add(new SqlParameter("@PageSize", pageSize));

            connection.Open();
            var scalar = countCommand.ExecuteScalar();
            totalCount = scalar == null ? 0 : Convert.ToInt32(scalar);

            var reader = dataCommand.ExecuteReader();

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
                        IsHospitalized = reader.GetString(reader.GetOrdinal("IsHospitalized")),
                        AdmitDate = reader.IsDBNull(reader.GetOrdinal("AdmitDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("AdmitDate")),
                        DischargeDate = reader.IsDBNull(reader.GetOrdinal("DischargeDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DischargeDate")),
                        DischargeStatus = reader.IsDBNull(reader.GetOrdinal("DischargeStatus")) ? string.Empty : reader.GetString(reader.GetOrdinal("DischargeStatus")),
                        TransferHospital = reader.IsDBNull(reader.GetOrdinal("TransferHospital")) ? string.Empty : reader.GetString(reader.GetOrdinal("TransferHospital")),
                        OtherDischargeStatus = reader.IsDBNull(reader.GetOrdinal("OtherDischargeStatus")) ? string.Empty : reader.GetString(reader.GetOrdinal("OtherDischargeStatus")),
                        AdmitHospital = reader.IsDBNull(reader.GetOrdinal("AdmitHospital")) ? string.Empty : reader.GetString(reader.GetOrdinal("AdmitHospital")),
                        Occupation = reader.IsDBNull(reader.GetOrdinal("Occupation")) ? string.Empty : reader.GetString(reader.GetOrdinal("Occupation")),
                        HasMajorInjury = reader.GetBoolean(reader.GetOrdinal("HasMajorInjury")),
                        HasDisability = reader.GetBoolean(reader.GetOrdinal("HasDisability")),
                        LastModifiedAt = reader.GetDateTime(reader.GetOrdinal("LastModifiedAt"))
                    };

                    result.Add(patient);
                }
            }

            connection.Close();

            return Task.FromResult((result, totalCount));
        }
        public Task<List<PatientDBModel>> QueryPatientList(long? patientId = null, string? idNo = null, string? familyName = null, string? givenName = null, string? dischargeStatus = null, string? transferHospital = null, string? occupation = null, bool? hasMajorInjury = null, bool? hasDisability = null)
        {
            var result = new List<PatientDBModel>();
            using var connection = new SqlConnection(ConnStr);
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
                                    , IsHospitalized
                                    , AdmitDate
                                    , DischargeDate
                                    , DischargeStatus
                                    , TransferHospital
                                    , OtherDischargeStatus
                                    , AdmitHospital
                                    , Occupation
                                    , HasMajorInjury
                                    , HasDisability
                                    , LastModifiedAt
                            FROM DB1.dbo.Patient
                            WHERE 1=1 ";

            queryStr += " AND Active = @Active";
            param.Add(new SqlParameter("@Active", true));

            if (patientId != null)
            {
                queryStr += " AND PatientId = @PatientId ";
                param.Add(new SqlParameter("@PatientId", patientId));
            }

            if (!string.IsNullOrWhiteSpace(idNo))
            {
                queryStr += " AND IdNo = @IdNo ";
                param.Add(new SqlParameter("@IdNo", idNo));
            }

            if (!string.IsNullOrWhiteSpace(familyName))
            {
                queryStr += " AND FamilyName LIKE '%' + @FamilyName + '%'";
                param.Add(new SqlParameter("@FamilyName", familyName));
            }

            if (!string.IsNullOrWhiteSpace(givenName))
            {
                queryStr += " AND GivenName LIKE '%' + @GivenName + '%'";
                param.Add(new SqlParameter("@GivenName", givenName));
            }

            if (!string.IsNullOrWhiteSpace(dischargeStatus))
            {
                queryStr += " AND DischargeStatus = @DischargeStatus ";
                param.Add(new SqlParameter("@DischargeStatus", dischargeStatus));
            }

            if (!string.IsNullOrWhiteSpace(transferHospital))
            {
                queryStr += " AND TransferHospital = @TransferHospital ";
                param.Add(new SqlParameter("@TransferHospital", transferHospital));
            }

            if (!string.IsNullOrWhiteSpace(occupation))
            {
                queryStr += " AND Occupation LIKE '%' + @Occupation + '%'";
                param.Add(new SqlParameter("@Occupation", occupation));
            }

            if (hasMajorInjury.HasValue)
            {
                queryStr += " AND HasMajorInjury = @HasMajorInjury ";
                param.Add(new SqlParameter("@HasMajorInjury", hasMajorInjury.Value));
            }

            if (hasDisability.HasValue)
            {
                queryStr += " AND HasDisability = @HasDisability ";
                param.Add(new SqlParameter("@HasDisability", hasDisability.Value));
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
                        IsHospitalized = reader.GetString(reader.GetOrdinal("IsHospitalized")),
                        AdmitDate = reader.IsDBNull(reader.GetOrdinal("AdmitDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("AdmitDate")),
                        DischargeDate = reader.IsDBNull(reader.GetOrdinal("DischargeDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DischargeDate")),
                        DischargeStatus = reader.IsDBNull(reader.GetOrdinal("DischargeStatus")) ? null : reader.GetString(reader.GetOrdinal("DischargeStatus")),
                        TransferHospital = reader.IsDBNull(reader.GetOrdinal("TransferHospital")) ? null : reader.GetString(reader.GetOrdinal("TransferHospital")),
                        OtherDischargeStatus = reader.IsDBNull(reader.GetOrdinal("OtherDischargeStatus")) ? null : reader.GetString(reader.GetOrdinal("OtherDischargeStatus")),
                        AdmitHospital = reader.IsDBNull(reader.GetOrdinal("AdmitHospital")) ? null : reader.GetString(reader.GetOrdinal("AdmitHospital")),
                        Occupation = reader.IsDBNull(reader.GetOrdinal("Occupation")) ? null : reader.GetString(reader.GetOrdinal("Occupation")),
                        HasMajorInjury = reader.GetBoolean(reader.GetOrdinal("HasMajorInjury")),
                        HasDisability = reader.GetBoolean(reader.GetOrdinal("HasDisability")),
                        LastModifiedAt = reader.GetDateTime(reader.GetOrdinal("LastModifiedAt"))
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
                                   IsHospitalized = @IsHospitalized,
                                   AdmitDate = @AdmitDate,
                                   DischargeDate = @DischargeDate,
                                   DischargeStatus = @DischargeStatus,
                                   TransferHospital = @TransferHospital,
                                   OtherDischargeStatus = @OtherDischargeStatus, 
                                   AdmitHospital = @AdmitHospital,
                                   Occupation = @Occupation,
                                   HasMajorInjury = @HasMajorInjury,
                                   HasDisability = @HasDisability,
                                   LastModifiedAt = SYSDATETIME()
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
            command.Parameters.Add(new SqlParameter("@IsHospitalized", patient.IsHospitalized));
            command.Parameters.Add(new SqlParameter("@AdmitDate", patient.AdmitDate.HasValue ? patient.AdmitDate.Value.ToString("yyyy/MM/dd") : DBNull.Value));
            command.Parameters.Add(new SqlParameter("@DischargeDate", patient.DischargeDate.HasValue ? patient.DischargeDate.Value.ToString("yyyy/MM/dd") : DBNull.Value));
            command.Parameters.Add(new SqlParameter("@DischargeStatus", string.IsNullOrWhiteSpace(patient.DischargeStatus) ? DBNull.Value : patient.DischargeStatus));
            command.Parameters.Add(new SqlParameter("@TransferHospital", string.IsNullOrWhiteSpace(patient.TransferHospital) ? DBNull.Value : patient.TransferHospital));
            command.Parameters.Add(new SqlParameter("@OtherDischargeStatus", string.IsNullOrWhiteSpace(patient.OtherDischargeStatus) ? DBNull.Value : patient.OtherDischargeStatus)); 
            command.Parameters.Add(new SqlParameter("@AdmitHospital", string.IsNullOrWhiteSpace(patient.AdmitHospital) ? DBNull.Value : patient.AdmitHospital)); 
            command.Parameters.Add(new SqlParameter("@Occupation", string.IsNullOrWhiteSpace(patient.Occupation) ? DBNull.Value : patient.Occupation));
            command.Parameters.Add(new SqlParameter("@HasMajorInjury", patient.HasMajorInjury));
            command.Parameters.Add(new SqlParameter("@HasDisability", patient.HasDisability));

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
                IsHospitalized = viewModel.IsHospitalized,
                AdmitDate = viewModel.AdmitDate,
                DischargeDate = viewModel.DischargeDate,
                DischargeStatus = viewModel.DischargeStatus,
                TransferHospital = viewModel.TransferHospital,
                OtherDischargeStatus = viewModel.OtherDischargeStatus,
                AdmitHospital = viewModel.AdmitHospital,
                Occupation = viewModel.Occupation,
                HasMajorInjury = viewModel.HasMajorInjury,
                HasDisability = viewModel.HasDisability,
                LastModifiedAt = viewModel.LastModifiedAt
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
                IsHospitalized = dbModel.IsHospitalized,
                AdmitDate = dbModel.AdmitDate,
                DischargeDate = dbModel.DischargeDate,
                DischargeStatus = dbModel.DischargeStatus,
                TransferHospital = dbModel.TransferHospital,
                OtherDischargeStatus = dbModel.OtherDischargeStatus,
                AdmitHospital = dbModel.AdmitHospital,
                Occupation = dbModel.Occupation,
                HasMajorInjury = dbModel.HasMajorInjury,
                HasDisability = dbModel.HasDisability,
                LastModifiedAt = dbModel.LastModifiedAt
            };
        }

        #endregion Private
    }
}




