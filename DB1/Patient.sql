CREATE TABLE [dbo].[Patient]
(
	[PatientId] BIGINT IDENTITY (1, 1) NOT FOR REPLICATION NOT NULL,
	[IdNo] VARCHAR(10) NOT NULL,
	CONSTRAINT UQ_Patient_IdNo UNIQUE ([IdNo]),
	[Active] BIT NOT NULL DEFAULT 1,
	[FamilyName] NVARCHAR(10) NOT NULL,
	[GivenName] NVARCHAR(50) NOT NULL,
	[Telecom] VARCHAR(10) NOT NULL,
	[Gender] VARCHAR(10) NOT NULL,
	[Birthday] DATE NOT NULL,
	[Address] NVARCHAR(100) NOT NULL,
	[IsHospitalized] CHAR(1) NOT NULL,
	[AdmitDate] DATE NULL,
	[DischargeDate] DATE NULL,
	[DischargeStatus] NVARCHAR(20) NULL,
	[OtherDischargeStatus] NVARCHAR(100) NULL,
	[AdmitHospital] NVARCHAR(100) NULL,
	[TransferHospital] NVARCHAR(100) NULL,
	[Occupation] NVARCHAR(100) NULL,
	[HasMajorIllness] BIT NOT NULL CONSTRAINT DF_Patient_HasMajorIllness DEFAULT 0,
	[HasDisability] BIT NOT NULL CONSTRAINT DF_Patient_HasDisability DEFAULT 0,
	[LastModified] DATETIME NOT NULL CONSTRAINT DF_Patient_LastModified DEFAULT (GETDATE())
)

GO
CREATE TRIGGER TRG_Patient_Update_LastModified
ON dbo.Patient
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Patient
    SET LastModified = GETDATE()
    FROM Inserted i
    WHERE dbo.Patient.PatientId = i.PatientId;
END
GO
