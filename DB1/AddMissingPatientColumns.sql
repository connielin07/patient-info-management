-- Adds missing Patient columns if they don't exist (safe to run multiple times)
-- This aligns the DB with application code that reads/writes OtherDischargeStatus and AdmitHospital.

IF COL_LENGTH('dbo.Patient', 'OtherDischargeStatus') IS NULL
BEGIN
    ALTER TABLE dbo.Patient
        ADD OtherDischargeStatus NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH('dbo.Patient', 'AdmitHospital') IS NULL
BEGIN
    ALTER TABLE dbo.Patient
        ADD AdmitHospital NVARCHAR(100) NULL;
END
GO
