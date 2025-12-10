-- Seed dummy patients (25 rows) for pagination demo
-- Target DB: DB1, table: dbo.Patient
-- Example run (adjust server if needed):
-- sqlcmd -S LAPTOP-NT4HL4A3\SQLEXPRESS -d DB1 -i SeedPatients.sql

INSERT INTO dbo.Patient
    (IdNo, Active, FamilyName, GivenName, Telecom, Gender, Birthday, Address, IsHospitalized, AdmitDate, DischargeDate, DischargeStatus, TransferHospital, Occupation, HasMajorInjury, HasDisability)
VALUES
    ('Z100000001',1,'Wang','Alice','0912001001','F','1990-01-02','Taipei Address 1','Y','2024-11-01','2024-11-08',N'娌荤?',NULL,'Nurse',0,0),
    ('Z100000002',1,'Lin','Bob','0912001002','M','1988-02-03','Taipei Address 2','N',NULL,NULL,NULL,NULL,'Engineer',0,0),
    ('Z100000003',1,'Chen','Carol','0912001003','F','1985-03-04','New Taipei Address 3','Y','2024-10-10','2024-10-18',N'?呮??瑰?',NULL,'Teacher',1,0),
    ('Z100000004',1,'Li','David','0912001004','M','1979-04-05','Taichung Address 4','N',NULL,NULL,NULL,NULL,'Driver',0,0),
    ('Z100000005',1,'Huang','Eve','0912001005','F','1992-05-06','Kaohsiung Address 5','Y','2024-09-05','2024-09-12',N'杞夐櫌','Kaohsiung CGH','Pharmacist',0,1),
    ('Z100000006',1,'Wu','Frank','0912001006','M','1984-06-07','Tainan Address 6','N',NULL,NULL,NULL,NULL,'Chef',0,0),
    ('Z100000007',1,'Chang','Grace','0912001007','F','1995-07-08','Keelung Address 7','Y','2024-08-15','2024-08-22',N'涓嶈?',NULL,'Student',0,0),
    ('Z100000008',1,'Chou','Henry','0912001008','M','1987-08-09','Hsinchu Address 8','Y','2024-07-10','2024-07-16',N'娌荤?',NULL,'Analyst',0,0),
    ('Z100000009',1,'Cheng','Ivy','0912001009','F','1998-09-10','Miaoli Address 9','N',NULL,NULL,NULL,NULL,'Designer',0,1),
    ('Z100000010',1,'Hsu','Jack','0912001010','M','1981-10-11','Changhua Address 10','Y','2024-06-01','2024-06-09',N'杞夐櫌','Taichung VGH','Officer',0,0),
    ('Z100000011',1,'Chien','Kelly','0912001011','F','1983-11-12','Chiayi Address 11','N',NULL,NULL,NULL,NULL,'Nurse',0,0),
    ('Z100000012',1,'Hsiao','Leo','0912001012','M','1993-12-13','Pingtung Address 12','Y','2024-05-02','2024-05-10',N'?呮??瑰?',NULL,'Engineer',1,0),
    ('Z100000013',1,'Tsao','Mia','0912001013','F','1970-01-14','Taitung Address 13','N',NULL,NULL,NULL,NULL,'Teacher',0,0),
    ('Z100000014',1,'Yu','Nick','0912001014','M','1987-02-15','Hualien Address 14','Y','2024-04-01','2024-04-08',N'杞夐櫌','Hualien TzuChi','Driver',0,0),
    ('Z100000015',1,'Wen','Olivia','0912001015','F','1991-03-16','Yilan Address 15','N',NULL,NULL,NULL,NULL,'Accountant',0,0),
    ('Z100000016',1,'Chiang','Peter','0912001016','M','1979-04-17','Yunlin Address 16','Y','2024-03-01','2024-03-09',N'涓嶈?',NULL,'Engineer',0,1),
    ('Z100000017',1,'Kao','Queenie','0912001017','F','1996-05-18','Nantou Address 17','N',NULL,NULL,NULL,NULL,'Student',0,0),
    ('Z100000018',1,'Kung','Ryan','0912001018','M','1982-06-19','Kinmen Address 18','Y','2024-02-10','2024-02-18',N'娌荤?',NULL,'Chef',0,0),
    ('Z100000019',1,'Chung','Sophia','0912001019','F','1994-07-20','Penghu Address 19','N',NULL,NULL,NULL,NULL,'Designer',0,0),
    ('Z100000020',1,'Su','Tom','0912001020','M','1980-08-21','Chiayi County Address 20','Y','2024-01-05','2024-01-12',N'杞夐櫌','Tainan NCKU','Officer',1,0),
    ('Z100000021',1,'Tai','Una','0912001021','F','1998-09-22','Pingtung Town Address 21','N',NULL,NULL,NULL,NULL,'Nurse',0,0),
    ('Z100000022',1,'Fan','Victor','0912001022','M','1976-10-23','Hsinchu County Address 22','Y','2023-12-02','2023-12-09',N'?呮??瑰?',NULL,'Engineer',0,0),
    ('Z100000023',1,'Feng','Wendy','0912001023','F','1986-11-24','Keelung Anle Address 23','N',NULL,NULL,NULL,NULL,'Teacher',0,1),
    ('Z100000024',1,'Han','Xavier','0912001024','M','1997-12-25','Taipei Xinyi Address 24','Y','2023-11-03','2023-11-11',N'娌荤?',NULL,'Accountant',0,0),
    ('Z100000025',1,'Luo','Yuki','0912001025','F','1990-01-26','Tainan East Address 25','N',NULL,NULL,NULL,NULL,'Analyst',0,0);
