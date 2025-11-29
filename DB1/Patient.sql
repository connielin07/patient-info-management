CREATE TABLE [dbo].[Patient]
(
	[PatientId] BIGINT IDENTITY (1, 1) NOT FOR REPLICATION NOT NULL,
	[IdNo] VARCHAR(10) NOT NULL,
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
	[TransferHospital] NVARCHAR(100) NULL
)
