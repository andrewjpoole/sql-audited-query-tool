-- =====================================================================================
-- SQL Audited Query Tool - Sample Database Schema and Test Data
-- Cash Deposit Platform with Partner Banks
-- =====================================================================================
-- This script creates a realistic schema for a cash deposit platform that partners
-- with multiple banks to accept customer deposits. It includes intentional data
-- anomalies and suspicious patterns that can be discovered using the query tool.
-- =====================================================================================

-- Ensure the database exists (Aspire names it "db")
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'db')
BEGIN
    CREATE DATABASE [db];
END
GO

USE [db];
GO

-- =====================================================================================
-- SCHEMA DEFINITION
-- =====================================================================================

-- Drop tables in reverse dependency order for idempotency
IF OBJECT_ID('AuditLog', 'U') IS NOT NULL DROP TABLE AuditLog;
IF OBJECT_ID('Reconciliation', 'U') IS NOT NULL DROP TABLE Reconciliation;
IF OBJECT_ID('Deposits', 'U') IS NOT NULL DROP TABLE Deposits;
IF OBJECT_ID('Fees', 'U') IS NOT NULL DROP TABLE Fees;
IF OBJECT_ID('Accounts', 'U') IS NOT NULL DROP TABLE Accounts;
IF OBJECT_ID('DepositLocations', 'U') IS NOT NULL DROP TABLE DepositLocations;
IF OBJECT_ID('Users', 'U') IS NOT NULL DROP TABLE Users;
IF OBJECT_ID('Partners', 'U') IS NOT NULL DROP TABLE Partners;
GO

-- =====================================================================================
-- Table: Partners
-- Partner banks and financial institutions that use the deposit platform
-- =====================================================================================
CREATE TABLE Partners (
    PartnerID INT IDENTITY(1,1) PRIMARY KEY,
    PartnerCode NVARCHAR(20) NOT NULL UNIQUE,
    PartnerName NVARCHAR(200) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
    OnboardedDate DATE NOT NULL,
    ApiKey NVARCHAR(100) NULL,
    SettlementAccountNumber NVARCHAR(50) NULL,
    ContactEmail NVARCHAR(100) NULL,
    ContactPhone NVARCHAR(20) NULL,
    FeePercentage DECIMAL(5,4) NOT NULL DEFAULT 0.0050,
    DailyDepositLimit DECIMAL(18,2) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_Partners_Status ON Partners(Status);
GO

-- =====================================================================================
-- Table: Users
-- Platform operators and staff who manage the system
-- =====================================================================================
CREATE TABLE Users (
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    FullName NVARCHAR(100) NOT NULL,
    Role NVARCHAR(30) NOT NULL,
    Department NVARCHAR(50) NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
    LastLoginAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_Users_Role ON Users(Role);
GO

-- =====================================================================================
-- Table: DepositLocations
-- Physical locations where customers can make deposits (branches, ATMs, kiosks)
-- =====================================================================================
CREATE TABLE DepositLocations (
    LocationID INT IDENTITY(1,1) PRIMARY KEY,
    LocationCode NVARCHAR(20) NOT NULL UNIQUE,
    LocationType NVARCHAR(20) NOT NULL,
    LocationName NVARCHAR(200) NOT NULL,
    Address NVARCHAR(300) NULL,
    City NVARCHAR(100) NOT NULL,
    State NVARCHAR(50) NOT NULL,
    PostalCode NVARCHAR(20) NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
    OpeningHours NVARCHAR(100) NULL,
    MaxDepositAmount DECIMAL(18,2) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_DepositLocations_City ON DepositLocations(City);
CREATE INDEX IX_DepositLocations_Status ON DepositLocations(Status);
GO

-- =====================================================================================
-- Table: Accounts
-- Customer accounts registered on the platform
-- =====================================================================================
CREATE TABLE Accounts (
    AccountID INT IDENTITY(1,1) PRIMARY KEY,
    PartnerID INT NOT NULL,
    AccountNumber NVARCHAR(50) NOT NULL,
    HolderName NVARCHAR(200) NOT NULL,
    AccountType NVARCHAR(30) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
    Balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    Email NVARCHAR(100) NULL,
    Phone NVARCHAR(20) NULL,
    KycStatus NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    DailyDepositLimit DECIMAL(18,2) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Accounts_Partners FOREIGN KEY (PartnerID) REFERENCES Partners(PartnerID),
    CONSTRAINT UQ_Account_Number_Partner UNIQUE (PartnerID, AccountNumber)
);

CREATE INDEX IX_Accounts_PartnerID ON Accounts(PartnerID);
CREATE INDEX IX_Accounts_Status ON Accounts(Status);
CREATE INDEX IX_Accounts_AccountNumber ON Accounts(AccountNumber);
GO

-- =====================================================================================
-- Table: Fees
-- Fee schedules per partner and deposit type
-- =====================================================================================
CREATE TABLE Fees (
    FeeID INT IDENTITY(1,1) PRIMARY KEY,
    PartnerID INT NOT NULL,
    DepositType NVARCHAR(30) NOT NULL,
    FeeType NVARCHAR(20) NOT NULL,
    FeeAmount DECIMAL(18,2) NULL,
    FeePercentage DECIMAL(5,4) NULL,
    MinFee DECIMAL(18,2) NULL,
    MaxFee DECIMAL(18,2) NULL,
    EffectiveFrom DATE NOT NULL,
    EffectiveTo DATE NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Fees_Partners FOREIGN KEY (PartnerID) REFERENCES Partners(PartnerID)
);

CREATE INDEX IX_Fees_PartnerID ON Fees(PartnerID);
GO

-- =====================================================================================
-- Table: Deposits
-- Cash deposit transactions processed through the platform
-- =====================================================================================
CREATE TABLE Deposits (
    DepositID INT IDENTITY(1,1) PRIMARY KEY,
    AccountID INT NOT NULL,
    LocationID INT NULL,
    ReferenceNumber NVARCHAR(50) NOT NULL UNIQUE,
    DepositType NVARCHAR(30) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    FeeAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    NetAmount DECIMAL(18,2) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    StatusReason NVARCHAR(500) NULL,
    DepositedBy NVARCHAR(200) NULL,
    ProcessedBy INT NULL,
    DepositDate DATETIME2 NOT NULL,
    ProcessedDate DATETIME2 NULL,
    SettledDate DATETIME2 NULL,
    Notes NVARCHAR(1000) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Deposits_Accounts FOREIGN KEY (AccountID) REFERENCES Accounts(AccountID),
    CONSTRAINT FK_Deposits_Locations FOREIGN KEY (LocationID) REFERENCES DepositLocations(LocationID),
    CONSTRAINT FK_Deposits_ProcessedBy FOREIGN KEY (ProcessedBy) REFERENCES Users(UserID)
);

CREATE INDEX IX_Deposits_AccountID ON Deposits(AccountID);
CREATE INDEX IX_Deposits_LocationID ON Deposits(LocationID);
CREATE INDEX IX_Deposits_Status ON Deposits(Status);
CREATE INDEX IX_Deposits_DepositDate ON Deposits(DepositDate);
CREATE INDEX IX_Deposits_ReferenceNumber ON Deposits(ReferenceNumber);
GO

-- =====================================================================================
-- Table: Reconciliation
-- Daily reconciliation records between platform and partner banks
-- =====================================================================================
CREATE TABLE Reconciliation (
    ReconciliationID INT IDENTITY(1,1) PRIMARY KEY,
    PartnerID INT NOT NULL,
    ReconciliationDate DATE NOT NULL,
    PlatformDepositCount INT NOT NULL,
    PlatformDepositTotal DECIMAL(18,2) NOT NULL,
    PlatformFeeTotal DECIMAL(18,2) NOT NULL,
    PartnerDepositCount INT NULL,
    PartnerDepositTotal DECIMAL(18,2) NULL,
    PartnerFeeTotal DECIMAL(18,2) NULL,
    CountDiscrepancy INT NOT NULL DEFAULT 0,
    AmountDiscrepancy DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    ReconciledBy INT NULL,
    ReconciledAt DATETIME2 NULL,
    Notes NVARCHAR(1000) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Reconciliation_Partners FOREIGN KEY (PartnerID) REFERENCES Partners(PartnerID),
    CONSTRAINT FK_Reconciliation_ReconciledBy FOREIGN KEY (ReconciledBy) REFERENCES Users(UserID),
    CONSTRAINT UQ_Reconciliation_Partner_Date UNIQUE (PartnerID, ReconciliationDate)
);

CREATE INDEX IX_Reconciliation_Status ON Reconciliation(Status);
CREATE INDEX IX_Reconciliation_Date ON Reconciliation(ReconciliationDate);
GO

-- =====================================================================================
-- Table: AuditLog
-- System audit trail tracking all significant actions
-- =====================================================================================
CREATE TABLE AuditLog (
    AuditID BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NULL,
    Action NVARCHAR(100) NOT NULL,
    EntityType NVARCHAR(50) NOT NULL,
    EntityID NVARCHAR(50) NULL,
    OldValue NVARCHAR(MAX) NULL,
    NewValue NVARCHAR(MAX) NULL,
    IPAddress NVARCHAR(45) NULL,
    UserAgent NVARCHAR(500) NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_AuditLog_Users FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

CREATE INDEX IX_AuditLog_UserID ON AuditLog(UserID);
CREATE INDEX IX_AuditLog_Timestamp ON AuditLog(Timestamp);
CREATE INDEX IX_AuditLog_Action ON AuditLog(Action);
GO

-- =====================================================================================
-- TEST DATA
-- =====================================================================================

PRINT 'Inserting test data...';
GO

-- =====================================================================================
-- Partners - 8 partner banks
-- DATA ERRORS:
--   Partner 4 (PSB) has a negative fee percentage (-0.0020)
--   Partner 6 (ACB) is Suspended but still has an active ApiKey (should be revoked)
--   Partner 8 (SWB) is Onboarding but deposits are flowing through its accounts
-- =====================================================================================
SET IDENTITY_INSERT Partners ON;

INSERT INTO Partners (PartnerID, PartnerCode, PartnerName, Status, OnboardedDate, ApiKey, SettlementAccountNumber, ContactEmail, ContactPhone, FeePercentage, DailyDepositLimit, CreatedAt, UpdatedAt)
VALUES
    (1, 'FNB', 'First National Bank',       'Active',     '2023-01-15', 'fnb-api-key-8a7f9b2e1c4d', '9876543210001', 'integration@firstnational.example',    '+1-555-0100', 0.0045,  5000000.00, DATEADD(DAY, -450, GETUTCDATE()), DATEADD(DAY, -10, GETUTCDATE())),
    (2, 'CUB', 'Community United Bank',     'Active',     '2023-03-22', 'cub-api-key-3d5e8f1a9b6c', '9876543210002', 'api@communityunited.example',          '+1-555-0101', 0.0050,  3000000.00, DATEADD(DAY, -380, GETUTCDATE()), DATEADD(DAY, -5, GETUTCDATE())),
    (3, 'MTB', 'Metro Trust Bank',          'Active',     '2023-06-10', 'mtb-api-key-7b4c2d9e6f1a', '9876543210003', 'ops@metrotrust.example',               '+1-555-0102', 0.0055,  2500000.00, DATEADD(DAY, -300, GETUTCDATE()), DATEADD(DAY, -2, GETUTCDATE())),
    (4, 'PSB', 'Pioneer Savings Bank',      'Active',     '2023-08-05', 'psb-api-key-5e9f1a3c8d7b', '9876543210004', 'support@pioneersavings.example',       '+1-555-0103',-0.0020,  4000000.00, DATEADD(DAY, -240, GETUTCDATE()), DATEADD(DAY, -15, GETUTCDATE())),
    (5, 'HFB', 'Heritage Financial Bank',   'Active',     '2024-01-12', 'hfb-api-key-2c6d9e1f4a8b', '9876543210005', 'tech@heritagefinancial.example',       '+1-555-0104', 0.0052,  3500000.00, DATEADD(DAY, -80, GETUTCDATE()),  DATEADD(DAY, -1, GETUTCDATE())),
    (6, 'ACB', 'Atlantic Commerce Bank',    'Suspended',  '2023-04-18', 'acb-api-key-9e1f3a7c5d2b', '9876543210006', 'compliance@atlanticcommerce.example',  '+1-555-0105', 0.0060,  2000000.00, DATEADD(DAY, -350, GETUTCDATE()), DATEADD(DAY, -30, GETUTCDATE())),
    (7, 'VCU', 'Valley Credit Union',       'Active',     '2023-11-20', 'vcu-api-key-4a8b2c9d6e1f', '9876543210007', 'info@valleycreditunion.example',       '+1-555-0106', 0.0047,  1500000.00, DATEADD(DAY, -130, GETUTCDATE()), DATEADD(DAY, -7, GETUTCDATE())),
    (8, 'SWB', 'Southwest Business Bank',   'Onboarding', '2024-03-01', NULL,                        NULL,            'onboarding@southwestbusiness.example', '+1-555-0107', 0.0050,  5000000.00, DATEADD(DAY, -15, GETUTCDATE()),  DATEADD(DAY, -1, GETUTCDATE()));

SET IDENTITY_INSERT Partners OFF;
GO

-- =====================================================================================
-- Users - Platform operators
-- DATA ERROR:
--   User 8 (tmiller) is Inactive but has a LastLoginAt within the last 3 days
-- =====================================================================================
SET IDENTITY_INSERT Users ON;

INSERT INTO Users (UserID, Username, Email, FullName, Role, Department, Status, LastLoginAt, CreatedAt)
VALUES
    (1, 'admin',    'admin@cashplatform.example',    'System Administrator', 'Admin',    'IT',               'Active',   DATEADD(HOUR, -2,  GETUTCDATE()), DATEADD(DAY, -500, GETUTCDATE())),
    (2, 'jsmith',   'jsmith@cashplatform.example',   'John Smith',           'Operator', 'Operations',       'Active',   DATEADD(HOUR, -5,  GETUTCDATE()), DATEADD(DAY, -400, GETUTCDATE())),
    (3, 'mjones',   'mjones@cashplatform.example',   'Mary Jones',           'Support',  'Customer Support', 'Active',   DATEADD(HOUR, -1,  GETUTCDATE()), DATEADD(DAY, -350, GETUTCDATE())),
    (4, 'bwilson',  'bwilson@cashplatform.example',  'Bob Wilson',           'Finance',  'Finance',          'Active',   DATEADD(HOUR, -8,  GETUTCDATE()), DATEADD(DAY, -300, GETUTCDATE())),
    (5, 'sjohnson', 'sjohnson@cashplatform.example', 'Sarah Johnson',        'Auditor',  'Compliance',       'Active',   DATEADD(HOUR, -24, GETUTCDATE()), DATEADD(DAY, -250, GETUTCDATE())),
    (6, 'rdavis',   'rdavis@cashplatform.example',   'Robert Davis',         'Manager',  'Operations',       'Active',   DATEADD(HOUR, -3,  GETUTCDATE()), DATEADD(DAY, -450, GETUTCDATE())),
    (7, 'lbrown',   'lbrown@cashplatform.example',   'Lisa Brown',           'Operator', 'Operations',       'Active',   DATEADD(DAY,  -2,  GETUTCDATE()), DATEADD(DAY, -200, GETUTCDATE())),
    (8, 'tmiller',  'tmiller@cashplatform.example',  'Tom Miller',           'Support',  'Customer Support', 'Inactive', DATEADD(DAY,  -3,  GETUTCDATE()), DATEADD(DAY, -180, GETUTCDATE()));

SET IDENTITY_INSERT Users OFF;
GO

-- =====================================================================================
-- DepositLocations - 20 locations
-- DATA ERRORS:
--   Location 9 (Hollywood Walk ATM) is 'Maintenance' but deposits are still coming in
--   Location 20 (Seattle Downtown Branch) has MaxDepositAmount = 0.00
-- =====================================================================================
SET IDENTITY_INSERT DepositLocations ON;

INSERT INTO DepositLocations (LocationID, LocationCode, LocationType, LocationName, Address, City, State, PostalCode, Status, OpeningHours, MaxDepositAmount, CreatedAt)
VALUES
    (1,  'BR-NY-001',  'Branch',        'Manhattan Financial Center',  '1234 Broadway',               'New York',    'NY', '10001', 'Active',      'Mon-Fri 9AM-5PM',       50000.00, DATEADD(DAY, -400, GETUTCDATE())),
    (2,  'BR-NY-002',  'Branch',        'Brooklyn Heights Office',     '567 Atlantic Ave',            'Brooklyn',    'NY', '11217', 'Active',      'Mon-Fri 9AM-5PM',       50000.00, DATEADD(DAY, -380, GETUTCDATE())),
    (3,  'ATM-NY-001', 'ATM',           'Times Square ATM',            '789 7th Ave',                 'New York',    'NY', '10036', 'Active',      '24/7',                   10000.00, DATEADD(DAY, -350, GETUTCDATE())),
    (4,  'ATM-NY-002', 'ATM',           'Penn Station ATM',            '234 W 31st St',               'New York',    'NY', '10001', 'Active',      '24/7',                   10000.00, DATEADD(DAY, -340, GETUTCDATE())),
    (5,  'KSK-NY-001', 'Kiosk',         'JFK Airport Kiosk T4',       'JFK International Airport',   'Queens',      'NY', '11430', 'Active',      '24/7',                    5000.00, DATEADD(DAY, -300, GETUTCDATE())),
    (6,  'BR-LA-001',  'Branch',        'Downtown LA Branch',          '1000 Wilshire Blvd',          'Los Angeles', 'CA', '90017', 'Active',      'Mon-Fri 9AM-6PM',       75000.00, DATEADD(DAY, -280, GETUTCDATE())),
    (7,  'BR-LA-002',  'Branch',        'Santa Monica Branch',         '345 Ocean Ave',               'Santa Monica','CA', '90401', 'Active',      'Mon-Fri 9AM-5PM',       50000.00, DATEADD(DAY, -260, GETUTCDATE())),
    (8,  'ATM-LA-001', 'ATM',           'LAX Airport ATM',             'LAX Airport',                 'Los Angeles', 'CA', '90045', 'Active',      '24/7',                   10000.00, DATEADD(DAY, -250, GETUTCDATE())),
    (9,  'ATM-LA-002', 'ATM',           'Hollywood Walk ATM',          '6801 Hollywood Blvd',         'Hollywood',   'CA', '90028', 'Maintenance', '24/7',                   10000.00, DATEADD(DAY, -240, GETUTCDATE())),
    (10, 'BR-CHI-001', 'Branch',        'Chicago Loop Center',         '200 S Michigan Ave',          'Chicago',     'IL', '60604', 'Active',      'Mon-Fri 8:30AM-5:30PM', 60000.00, DATEADD(DAY, -220, GETUTCDATE())),
    (11, 'ATM-CHI-001','ATM',           'O''Hare Airport ATM T1',      'O''Hare International Airport','Chicago',    'IL', '60666', 'Active',      '24/7',                   10000.00, DATEADD(DAY, -200, GETUTCDATE())),
    (12, 'BR-HOU-001', 'Branch',        'Houston Energy Plaza',        '600 Travis St',               'Houston',     'TX', '77002', 'Active',      'Mon-Fri 9AM-5PM',       50000.00, DATEADD(DAY, -180, GETUTCDATE())),
    (13, 'ATM-HOU-001','ATM',           'IAH Airport ATM',             'Bush Intercontinental Airport','Houston',     'TX', '77032', 'Active',      '24/7',                   10000.00, DATEADD(DAY, -170, GETUTCDATE())),
    (14, 'BR-MIA-001', 'Branch',        'Miami Brickell Branch',       '1200 Brickell Ave',           'Miami',       'FL', '33131', 'Active',      'Mon-Fri 9AM-5PM',       50000.00, DATEADD(DAY, -150, GETUTCDATE())),
    (15, 'ATM-MIA-001','ATM',           'South Beach ATM',             '1001 Ocean Dr',               'Miami Beach', 'FL', '33139', 'Active',      '24/7',                   10000.00, DATEADD(DAY, -140, GETUTCDATE())),
    (16, 'AG-NY-001',  'AgentLocation', 'Quick Cash Agent - Harlem',   '125th St & Lenox Ave',        'New York',    'NY', '10027', 'Active',      'Mon-Sat 8AM-8PM',       20000.00, DATEADD(DAY, -120, GETUTCDATE())),
    (17, 'AG-LA-001',  'AgentLocation', 'EZ Deposit - Koreatown',      '3200 Wilshire Blvd',          'Los Angeles', 'CA', '90010', 'Active',      'Mon-Sun 9AM-9PM',       20000.00, DATEADD(DAY, -100, GETUTCDATE())),
    (18, 'KSK-CHI-001','Kiosk',         'Union Station Kiosk',         'Union Station',               'Chicago',     'IL', '60661', 'Active',      '24/7',                    5000.00, DATEADD(DAY, -80,  GETUTCDATE())),
    (19, 'ATM-PHX-001','ATM',           'Phoenix Sky Harbor ATM',      'Phoenix Sky Harbor Airport',  'Phoenix',     'AZ', '85034', 'Active',      '24/7',                   10000.00, DATEADD(DAY, -60,  GETUTCDATE())),
    (20, 'BR-SEA-001', 'Branch',        'Seattle Downtown Branch',     '1500 4th Ave',                'Seattle',     'WA', '98101', 'Active',      'Mon-Fri 9AM-5PM',           0.00, DATEADD(DAY, -40,  GETUTCDATE()));

SET IDENTITY_INSERT DepositLocations OFF;
GO

-- =====================================================================================
-- Accounts - 50 customer accounts across partners
-- DATA ERRORS:
--   Account 15: Suspended but KycStatus still 'Verified'
--   Account 22: Currency = 'GBP' but all its deposits are in USD
--   Account 33: Suspended but has recent Completed deposits and high balance
--   Account 38-39: Belong to Partner 6 (Suspended) but accounts are Active
--   Account 42: Frozen with negative balance (-1250.00)
--   Account 47: KycStatus = 'RequiresUpdate' but created 300+ days ago (stale)
--   Accounts 48-50: KycStatus = 'Pending' but have already received deposits
-- =====================================================================================
SET IDENTITY_INSERT Accounts ON;

INSERT INTO Accounts (AccountID, PartnerID, AccountNumber, HolderName, AccountType, Status, Balance, Currency, Email, Phone, KycStatus, DailyDepositLimit, CreatedAt)
VALUES
    ( 1, 1, 'ACC-FNB-000001', 'Maria Garcia',         'Checking',   'Active',    12450.50, 'USD', 'mgarcia@example.com',        '+1-555-1001', 'Verified',       25000.00, DATEADD(DAY, -420, GETUTCDATE())),
    ( 2, 1, 'ACC-FNB-000002', 'Robert Taylor',        'Savings',    'Active',     8320.75, 'USD', 'rtaylor@example.com',        '+1-555-1002', 'Verified',       25000.00, DATEADD(DAY, -410, GETUTCDATE())),
    ( 3, 2, 'ACC-CUB-000001', 'Jennifer Martinez',    'Checking',   'Active',    22100.00, 'USD', 'jmartinez@example.com',      '+1-555-1003', 'Verified',       25000.00, DATEADD(DAY, -370, GETUTCDATE())),
    ( 4, 2, 'ACC-CUB-000002', 'Michael Brown',        'Business',   'Active',    87650.25, 'USD', 'mbrown@example.com',         '+1-555-1004', 'Verified',      100000.00, DATEADD(DAY, -365, GETUTCDATE())),
    ( 5, 3, 'ACC-MTB-000001', 'Jessica Wilson',       'Checking',   'Active',     3210.90, 'USD', 'jwilson@example.com',        '+1-555-1005', 'Verified',       25000.00, DATEADD(DAY, -290, GETUTCDATE())),
    ( 6, 3, 'ACC-MTB-000002', 'David Lee',            'Savings',    'Active',    15780.00, 'USD', 'dlee@example.com',           '+1-555-1006', 'Verified',       25000.00, DATEADD(DAY, -285, GETUTCDATE())),
    ( 7, 4, 'ACC-PSB-000001', 'Sarah Thomas',         'Checking',   'Active',     6540.30, 'USD', 'sthomas@example.com',        '+1-555-1007', 'Verified',       25000.00, DATEADD(DAY, -230, GETUTCDATE())),
    ( 8, 4, 'ACC-PSB-000002', 'Christopher White',    'Business',   'Active',    45200.00, 'USD', 'cwhite@example.com',         '+1-555-1008', 'Verified',      100000.00, DATEADD(DAY, -225, GETUTCDATE())),
    ( 9, 5, 'ACC-HFB-000001', 'Amanda Harris',        'Checking',   'Active',     9870.15, 'USD', 'aharris@example.com',        '+1-555-1009', 'Verified',       25000.00, DATEADD(DAY, -75,  GETUTCDATE())),
    (10, 5, 'ACC-HFB-000002', 'Daniel Clark',         'Savings',    'Active',    28340.60, 'USD', 'dclark@example.com',         '+1-555-1010', 'Verified',       25000.00, DATEADD(DAY, -70,  GETUTCDATE())),
    (11, 1, 'ACC-FNB-000003', 'Emily Lewis',          'Business',   'Active',    67200.00, 'USD', 'elewis@example.com',         '+1-555-1011', 'Verified',      100000.00, DATEADD(DAY, -390, GETUTCDATE())),
    (12, 1, 'ACC-FNB-000004', 'Matthew Robinson',     'Investment', 'Active',   152000.00, 'USD', 'mrobinson@example.com',      '+1-555-1012', 'Verified',       50000.00, DATEADD(DAY, -385, GETUTCDATE())),
    (13, 2, 'ACC-CUB-000003', 'Ashley Walker',        'Checking',   'Active',     4560.80, 'USD', 'awalker@example.com',        '+1-555-1013', 'Verified',       25000.00, DATEADD(DAY, -355, GETUTCDATE())),
    (14, 2, 'ACC-CUB-000004', 'Andrew Young',         'Savings',    'Active',    19200.40, 'USD', 'ayoung@example.com',         '+1-555-1014', 'Verified',       25000.00, DATEADD(DAY, -350, GETUTCDATE())),
    (15, 3, 'ACC-MTB-000003', 'Stephanie Hall',       'Checking',   'Suspended',  7890.00, 'USD', 'shall@example.com',          '+1-555-1015', 'Verified',       25000.00, DATEADD(DAY, -275, GETUTCDATE())),
    (16, 3, 'ACC-MTB-000004', 'Joshua Allen',         'Business',   'Active',    53400.75, 'USD', 'jallen@example.com',         '+1-555-1016', 'Verified',      100000.00, DATEADD(DAY, -270, GETUTCDATE())),
    (17, 4, 'ACC-PSB-000003', 'Michelle King',        'Checking',   'Active',     2150.25, 'USD', 'mking@example.com',          '+1-555-1017', 'Verified',       25000.00, DATEADD(DAY, -215, GETUTCDATE())),
    (18, 4, 'ACC-PSB-000004', 'Ryan Wright',          'Savings',    'Active',    11300.90, 'USD', 'rwright@example.com',        '+1-555-1018', 'Verified',       25000.00, DATEADD(DAY, -210, GETUTCDATE())),
    (19, 5, 'ACC-HFB-000003', 'Laura Scott',          'Checking',   'Active',     6780.35, 'USD', 'lscott@example.com',         '+1-555-1019', 'Verified',       25000.00, DATEADD(DAY, -65,  GETUTCDATE())),
    (20, 5, 'ACC-HFB-000004', 'James Anderson',       'Business',   'Active',    98700.00, 'USD', 'janderson@example.com',      '+1-555-1020', 'Verified',      100000.00, DATEADD(DAY, -60,  GETUTCDATE())),
    (21, 7, 'ACC-VCU-000001', 'Patricia Green',       'Checking',   'Active',     5430.20, 'USD', 'pgreen@example.com',         '+1-555-1021', 'Verified',       25000.00, DATEADD(DAY, -125, GETUTCDATE())),
    (22, 7, 'ACC-VCU-000002', 'Thomas Baker',         'Savings',    'Active',    14500.00, 'GBP', 'tbaker@example.com',         '+1-555-1022', 'Verified',       25000.00, DATEADD(DAY, -120, GETUTCDATE())),
    (23, 1, 'ACC-FNB-000005', 'Nancy Adams',          'Checking',   'Active',     3890.55, 'USD', 'nadams@example.com',         '+1-555-1023', 'Verified',       25000.00, DATEADD(DAY, -380, GETUTCDATE())),
    (24, 1, 'ACC-FNB-000006', 'Kevin Nelson',         'Savings',    'Active',    21600.10, 'USD', 'knelson@example.com',        '+1-555-1024', 'Verified',       25000.00, DATEADD(DAY, -375, GETUTCDATE())),
    (25, 2, 'ACC-CUB-000005', 'Sandra Carter',        'Business',   'Active',    72000.00, 'USD', 'scarter@example.com',        '+1-555-1025', 'Verified',      100000.00, DATEADD(DAY, -340, GETUTCDATE())),
    (26, 2, 'ACC-CUB-000006', 'Richard Mitchell',     'Checking',   'Active',     8100.45, 'USD', 'rmitchell@example.com',      '+1-555-1026', 'Verified',       25000.00, DATEADD(DAY, -335, GETUTCDATE())),
    (27, 3, 'ACC-MTB-000005', 'Betty Perez',          'Savings',    'Active',    16900.70, 'USD', 'bperez@example.com',         '+1-555-1027', 'Verified',       25000.00, DATEADD(DAY, -260, GETUTCDATE())),
    (28, 3, 'ACC-MTB-000006', 'Mark Roberts',         'Checking',   'Active',     4370.90, 'USD', 'mroberts@example.com',       '+1-555-1028', 'Verified',       25000.00, DATEADD(DAY, -255, GETUTCDATE())),
    (29, 4, 'ACC-PSB-000005', 'Dorothy Turner',       'Business',   'Active',    61500.00, 'USD', 'dturner@example.com',        '+1-555-1029', 'Verified',      100000.00, DATEADD(DAY, -200, GETUTCDATE())),
    (30, 4, 'ACC-PSB-000006', 'Steven Phillips',      'Checking',   'Active',     7250.15, 'USD', 'sphillips@example.com',      '+1-555-1030', 'Verified',       25000.00, DATEADD(DAY, -195, GETUTCDATE())),
    (31, 5, 'ACC-HFB-000005', 'Donna Campbell',       'Savings',    'Active',    13400.80, 'USD', 'dcampbell@example.com',      '+1-555-1031', 'Verified',       25000.00, DATEADD(DAY, -55,  GETUTCDATE())),
    (32, 5, 'ACC-HFB-000006', 'Kenneth Parker',       'Checking',   'Active',     9020.45, 'USD', 'kparker@example.com',        '+1-555-1032', 'Verified',       25000.00, DATEADD(DAY, -50,  GETUTCDATE())),
    (33, 7, 'ACC-VCU-000003', 'Helen Evans',          'Savings',    'Suspended', 31000.00, 'USD', 'hevans@example.com',         '+1-555-1033', 'Verified',       25000.00, DATEADD(DAY, -115, GETUTCDATE())),
    (34, 7, 'ACC-VCU-000004', 'George Edwards',       'Checking',   'Active',     5670.30, 'USD', 'gedwards@example.com',       '+1-555-1034', 'Verified',       25000.00, DATEADD(DAY, -110, GETUTCDATE())),
    (35, 1, 'ACC-FNB-000007', 'Carol Collins',        'Checking',   'Active',     2890.60, 'USD', 'ccollins@example.com',       '+1-555-1035', 'Verified',       25000.00, DATEADD(DAY, -360, GETUTCDATE())),
    (36, 2, 'ACC-CUB-000007', 'Frank Stewart',        'Business',   'Active',    54300.00, 'USD', 'fstewart@example.com',       '+1-555-1036', 'Verified',      100000.00, DATEADD(DAY, -320, GETUTCDATE())),
    (37, 3, 'ACC-MTB-000007', 'Virginia Morris',      'Savings',    'Active',    18700.50, 'USD', 'vmorris@example.com',        '+1-555-1037', 'Verified',       25000.00, DATEADD(DAY, -245, GETUTCDATE())),
    (38, 6, 'ACC-ACB-000001', 'Raymond Rogers',       'Checking',   'Active',    11200.00, 'USD', 'rrogers@example.com',        '+1-555-1038', 'Verified',       25000.00, DATEADD(DAY, -340, GETUTCDATE())),
    (39, 6, 'ACC-ACB-000002', 'Janet Reed',           'Savings',    'Active',     6500.75, 'USD', 'jreed@example.com',          '+1-555-1039', 'Verified',       25000.00, DATEADD(DAY, -335, GETUTCDATE())),
    (40, 1, 'ACC-FNB-000008', 'Henry Cook',           'Checking',   'Active',     4100.20, 'USD', 'hcook@example.com',          '+1-555-1040', 'Verified',       25000.00, DATEADD(DAY, -345, GETUTCDATE())),
    (41, 2, 'ACC-CUB-000008', 'Diana Morgan',         'Savings',    'Active',    22800.90, 'USD', 'dmorgan@example.com',        '+1-555-1041', 'Verified',       25000.00, DATEADD(DAY, -310, GETUTCDATE())),
    (42, 3, 'ACC-MTB-000008', 'Arthur Bell',          'Checking',   'Frozen',    -1250.00, 'USD', 'abell@example.com',          '+1-555-1042', 'Verified',       25000.00, DATEADD(DAY, -235, GETUTCDATE())),
    (43, 4, 'ACC-PSB-000007', 'Frances Murphy',       'Business',   'Active',    43100.50, 'USD', 'fmurphy@example.com',        '+1-555-1043', 'Verified',      100000.00, DATEADD(DAY, -185, GETUTCDATE())),
    (44, 5, 'ACC-HFB-000007', 'Jack Bailey',          'Checking',   'Active',     7640.35, 'USD', 'jbailey@example.com',        '+1-555-1044', 'Verified',       25000.00, DATEADD(DAY, -45,  GETUTCDATE())),
    (45, 7, 'ACC-VCU-000005', 'Grace Rivera',         'Savings',    'Active',    10200.60, 'USD', 'grivera@example.com',        '+1-555-1045', 'Verified',       25000.00, DATEADD(DAY, -100, GETUTCDATE())),
    (46, 1, 'ACC-FNB-000009', 'Albert Cooper',        'Checking',   'Active',     5560.00, 'USD', 'acooper@example.com',        '+1-555-1046', 'Verified',       25000.00, DATEADD(DAY, -330, GETUTCDATE())),
    (47, 2, 'ACC-CUB-000009', 'Irene Richardson',     'Savings',    'Active',     3200.80, 'USD', 'irichardson@example.com',    '+1-555-1047', 'RequiresUpdate', 25000.00, DATEADD(DAY, -300, GETUTCDATE())),
    (48, 3, 'ACC-MTB-000009', 'Philip Cox',           'Checking',   'Active',     1850.20, 'USD', 'pcox@example.com',           '+1-555-1048', 'Pending',        25000.00, DATEADD(DAY, -30,  GETUTCDATE())),
    (49, 4, 'ACC-PSB-000008', 'Teresa Howard',        'Savings',    'Active',     4710.55, 'USD', 'thoward@example.com',        '+1-555-1049', 'Pending',        25000.00, DATEADD(DAY, -25,  GETUTCDATE())),
    (50, 5, 'ACC-HFB-000008', 'Eugene Ward',          'Checking',   'Active',     2340.10, 'USD', 'eward@example.com',          '+1-555-1050', 'Pending',        25000.00, DATEADD(DAY, -20,  GETUTCDATE()));

SET IDENTITY_INSERT Accounts OFF;
GO

-- =====================================================================================
-- Fees - Fee schedules
-- DATA ERROR:
--   Fee 9 (Partner 4, BulkCash) has MinFee ($150) > MaxFee ($15) — inverted range
-- =====================================================================================
SET IDENTITY_INSERT Fees ON;

INSERT INTO Fees (FeeID, PartnerID, DepositType, FeeType, FeeAmount, FeePercentage, MinFee, MaxFee, EffectiveFrom, CreatedAt)
VALUES
    ( 1, 1, 'Cash',     'Percentage', NULL, 0.0045, 0.50,   25.00,  '2023-01-01', DATEADD(DAY, -450, GETUTCDATE())),
    ( 2, 1, 'Check',    'Fixed',      2.50, NULL,   NULL,   NULL,   '2023-01-01', DATEADD(DAY, -450, GETUTCDATE())),
    ( 3, 1, 'BulkCash', 'Percentage', NULL, 0.0035, 10.00, 100.00,  '2023-01-01', DATEADD(DAY, -450, GETUTCDATE())),
    ( 4, 2, 'Cash',     'Percentage', NULL, 0.0050, 0.50,   30.00,  '2023-03-01', DATEADD(DAY, -380, GETUTCDATE())),
    ( 5, 2, 'Check',    'Fixed',      3.00, NULL,   NULL,   NULL,   '2023-03-01', DATEADD(DAY, -380, GETUTCDATE())),
    ( 6, 3, 'Cash',     'Percentage', NULL, 0.0055, 0.75,   35.00,  '2023-06-01', DATEADD(DAY, -300, GETUTCDATE())),
    ( 7, 3, 'Transfer', 'Fixed',      5.00, NULL,   NULL,   NULL,   '2023-06-01', DATEADD(DAY, -300, GETUTCDATE())),
    ( 8, 4, 'Cash',     'Percentage', NULL, 0.0048, 0.50,   25.00,  '2023-08-01', DATEADD(DAY, -240, GETUTCDATE())),
    ( 9, 4, 'BulkCash', 'Percentage', NULL, 0.0040, 150.00,  15.00, '2023-08-01', DATEADD(DAY, -240, GETUTCDATE())),
    (10, 5, 'Cash',     'Percentage', NULL, 0.0052, 0.60,   28.00,  '2024-01-01', DATEADD(DAY, -80,  GETUTCDATE())),
    (11, 6, 'Cash',     'Percentage', NULL, 0.0060, 1.00,   40.00,  '2023-04-01', DATEADD(DAY, -350, GETUTCDATE())),
    (12, 7, 'Cash',     'Percentage', NULL, 0.0047, 0.50,   22.00,  '2023-11-01', DATEADD(DAY, -130, GETUTCDATE()));

SET IDENTITY_INSERT Fees OFF;
GO

-- =====================================================================================
-- Deposits - 200+ transactions with intentional data anomalies
--
-- SUSPICIOUS PATTERNS (discoverable with queries):
--   1. Structuring/smurfing: Deposits 150-153 — Account 25, same location,
--      4 deposits of ~$9,500-9,800 each on the same day (just under $10k CTR threshold)
--   2. Velocity abuse: Deposits 160-164 — Account 30, 5 x $4,900 deposits spread
--      across 5 different cities within the same day
--   3. Ghost deposits: Deposits 180-182 — Completed but ProcessedBy is NULL
--   4. Time-travel: Deposit 190 — SettledDate is BEFORE ProcessedDate
--   5. Zero-fee cash deposits: Deposits 195-198 — FeeAmount = $0.00 (should never be)
--   6. Frozen/Suspended account deposits: Deposits 200-203 go to Account 42 (Frozen)
--      and Account 33 (Suspended) but are marked Completed
--   7. Deposits against suspended partner: Deposits 210-212 go to Accounts 38-39
--      (Partner 6 / ACB which is Suspended)
--   8. Location mismatch: Deposit 205 placed at Location 9 (Maintenance status)
--   9. Possible duplicate: Deposits 206 & 207 — same account, location, amount,
--      3 minutes apart
-- =====================================================================================
SET IDENTITY_INSERT Deposits ON;

-- Normal completed deposits (bulk)
DECLARE @i INT = 1;
DECLARE @BaseDate DATETIME2 = DATEADD(DAY, -90, GETUTCDATE());

WHILE @i <= 140
BEGIN
    DECLARE @accId INT = ((@i - 1) % 46) + 1;
    DECLARE @locId INT = ((@i - 1) % 18) + 1;
    IF @locId >= 9 SET @locId = @locId + 1;
    DECLARE @amt DECIMAL(18,2) = CAST(500 + ((@i * 137) % 4500) AS DECIMAL(18,2));
    DECLARE @fee DECIMAL(18,2) = CAST(@amt * 0.0050 AS DECIMAL(18,2));
    IF @fee < 0.50 SET @fee = 0.50;
    IF @fee > 30.00 SET @fee = 30.00;

    DECLARE @depType NVARCHAR(30) = CASE (@i % 8)
        WHEN 0 THEN 'Check'
        WHEN 7 THEN 'BulkCash'
        ELSE 'Cash'
    END;

    DECLARE @depDate DATETIME2 = DATEADD(HOUR, (@i * 3) % 14 + 8, DATEADD(DAY, @i % 85, @BaseDate));
    DECLARE @procDate DATETIME2 = DATEADD(MINUTE, 45, @depDate);
    DECLARE @settlDate DATETIME2 = DATEADD(DAY, 1, @procDate);
    DECLARE @procBy INT = (@i % 4) + 2;

    INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
    VALUES (
        @i, @accId,
        CASE WHEN @depType = 'Transfer' THEN NULL ELSE @locId END,
        'DEP-2025-' + RIGHT('000000' + CAST(@i AS NVARCHAR), 6),
        @depType, @amt, 'USD', @fee, @amt - @fee,
        'Completed', NULL,
        'Customer Walk-in', @procBy, @depDate, @procDate, @settlDate, NULL,
        @depDate, @settlDate
    );
    SET @i = @i + 1;
END;

-- Failed deposits
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (141, 5,  3, 'DEP-2025-000141', 'Cash', 2500.00, 'USD', 12.50, 2487.50, 'Failed', 'Daily deposit limit exceeded for this account',                    'Customer Walk-in', 2, DATEADD(DAY, -45, GETUTCDATE()), DATEADD(DAY, -45, GETUTCDATE()), NULL, NULL, DATEADD(DAY, -45, GETUTCDATE()), DATEADD(DAY, -45, GETUTCDATE())),
    (142, 9,  8, 'DEP-2025-000142', 'Cash', 1800.00, 'USD',  9.00, 1791.00, 'Failed', 'Partner API timeout during deposit submission',                    'Customer Walk-in', 3, DATEADD(DAY, -38, GETUTCDATE()), DATEADD(DAY, -38, GETUTCDATE()), NULL, NULL, DATEADD(DAY, -38, GETUTCDATE()), DATEADD(DAY, -38, GETUTCDATE())),
    (143, 17, 4, 'DEP-2025-000143', 'Cash', 3200.00, 'USD', 16.00, 3184.00, 'Failed', 'Account validation failed - account not found in partner system',  'Customer Walk-in', 2, DATEADD(DAY, -30, GETUTCDATE()), DATEADD(DAY, -30, GETUTCDATE()), NULL, NULL, DATEADD(DAY, -30, GETUTCDATE()), DATEADD(DAY, -30, GETUTCDATE())),
    (144, 48, 1, 'DEP-2025-000144', 'Cash', 1500.00, 'USD',  7.50, 1492.50, 'Failed', 'KYC verification required before processing deposit',             'Customer Walk-in', 4, DATEADD(DAY, -22, GETUTCDATE()), DATEADD(DAY, -22, GETUTCDATE()), NULL, NULL, DATEADD(DAY, -22, GETUTCDATE()), DATEADD(DAY, -22, GETUTCDATE())),
    (145, 23, 6, 'DEP-2025-000145', 'Cash', 4100.00, 'USD', 20.50, 4079.50, 'Failed', 'Partner API timeout during deposit submission',                    'Customer Walk-in', 5, DATEADD(DAY, -15, GETUTCDATE()), DATEADD(DAY, -15, GETUTCDATE()), NULL, NULL, DATEADD(DAY, -15, GETUTCDATE()), DATEADD(DAY, -15, GETUTCDATE()));

-- Reversed deposits
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (146, 12, 1, 'DEP-2025-000146', 'Cash',  5200.00, 'USD', 25.00, 5175.00, 'Reversed', 'Customer request - duplicate deposit detected',                   'Customer Walk-in', 2, DATEADD(DAY, -52, GETUTCDATE()), DATEADD(DAY, -52, GETUTCDATE()), DATEADD(DAY, -51, GETUTCDATE()), NULL, DATEADD(DAY, -52, GETUTCDATE()), DATEADD(DAY, -51, GETUTCDATE())),
    (147, 26, 10,'DEP-2025-000147', 'Cash',  3100.00, 'USD', 15.50, 3084.50, 'Reversed', 'Fraudulent transaction flagged by partner bank',                  'Customer Walk-in', 3, DATEADD(DAY, -44, GETUTCDATE()), DATEADD(DAY, -43, GETUTCDATE()), DATEADD(DAY, -42, GETUTCDATE()), NULL, DATEADD(DAY, -44, GETUTCDATE()), DATEADD(DAY, -42, GETUTCDATE())),
    (148,  7, 14,'DEP-2025-000148', 'Check', 8500.00, 'USD',  2.50, 8497.50, 'Reversed', 'Reconciliation discrepancy - reversed per partner request',       'Customer Walk-in', 4, DATEADD(DAY, -35, GETUTCDATE()), DATEADD(DAY, -34, GETUTCDATE()), DATEADD(DAY, -33, GETUTCDATE()), NULL, DATEADD(DAY, -35, GETUTCDATE()), DATEADD(DAY, -33, GETUTCDATE())),
    (149, 41, 12,'DEP-2025-000149', 'Cash',  2750.00, 'USD', 13.75, 2736.25, 'Reversed', 'Customer request - wrong account deposited to',                   'Customer Walk-in', 2, DATEADD(DAY, -20, GETUTCDATE()), DATEADD(DAY, -19, GETUTCDATE()), DATEADD(DAY, -18, GETUTCDATE()), NULL, DATEADD(DAY, -20, GETUTCDATE()), DATEADD(DAY, -18, GETUTCDATE()));

-- SUSPICIOUS: Structuring (smurfing) — 4 deposits just under $10k threshold
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (150, 25, 1, 'DEP-2025-000150', 'Cash', 9500.00, 'USD', 30.00, 9470.00, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(DAY, -10, GETUTCDATE()), DATEADD(HOUR, 1, DATEADD(DAY, -10, GETUTCDATE())), DATEADD(DAY, -9, GETUTCDATE()), NULL, DATEADD(DAY, -10, GETUTCDATE()), DATEADD(DAY, -9, GETUTCDATE())),
    (151, 25, 1, 'DEP-2025-000151', 'Cash', 9600.00, 'USD', 30.00, 9570.00, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(HOUR, 2, DATEADD(DAY, -10, GETUTCDATE())), DATEADD(HOUR, 3, DATEADD(DAY, -10, GETUTCDATE())), DATEADD(DAY, -9, GETUTCDATE()), NULL, DATEADD(DAY, -10, GETUTCDATE()), DATEADD(DAY, -9, GETUTCDATE())),
    (152, 25, 1, 'DEP-2025-000152', 'Cash', 9700.00, 'USD', 30.00, 9670.00, 'Completed', NULL, 'Customer Walk-in', 7, DATEADD(HOUR, 4, DATEADD(DAY, -10, GETUTCDATE())), DATEADD(HOUR, 5, DATEADD(DAY, -10, GETUTCDATE())), DATEADD(DAY, -9, GETUTCDATE()), NULL, DATEADD(DAY, -10, GETUTCDATE()), DATEADD(DAY, -9, GETUTCDATE())),
    (153, 25, 1, 'DEP-2025-000153', 'Cash', 9800.00, 'USD', 30.00, 9770.00, 'Completed', NULL, 'Customer Walk-in', 7, DATEADD(HOUR, 6, DATEADD(DAY, -10, GETUTCDATE())), DATEADD(HOUR, 7, DATEADD(DAY, -10, GETUTCDATE())), DATEADD(DAY, -9, GETUTCDATE()), NULL, DATEADD(DAY, -10, GETUTCDATE()), DATEADD(DAY, -9, GETUTCDATE()));

-- SUSPICIOUS: Velocity abuse — 5 deposits at different locations same day
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (160, 30, 1,  'DEP-2025-000160', 'Cash', 4900.00, 'USD', 24.50, 4875.50, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(DAY, -7, GETUTCDATE()), DATEADD(HOUR, 1, DATEADD(DAY, -7, GETUTCDATE())), DATEADD(DAY, -6, GETUTCDATE()), NULL, DATEADD(DAY, -7, GETUTCDATE()), DATEADD(DAY, -6, GETUTCDATE())),
    (161, 30, 3,  'DEP-2025-000161', 'Cash', 4900.00, 'USD', 24.50, 4875.50, 'Completed', NULL, 'Customer Walk-in', 3, DATEADD(HOUR, 2, DATEADD(DAY, -7, GETUTCDATE())), DATEADD(HOUR, 3, DATEADD(DAY, -7, GETUTCDATE())), DATEADD(DAY, -6, GETUTCDATE()), NULL, DATEADD(DAY, -7, GETUTCDATE()), DATEADD(DAY, -6, GETUTCDATE())),
    (162, 30, 6,  'DEP-2025-000162', 'Cash', 4900.00, 'USD', 24.50, 4875.50, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(HOUR, 5, DATEADD(DAY, -7, GETUTCDATE())), DATEADD(HOUR, 6, DATEADD(DAY, -7, GETUTCDATE())), DATEADD(DAY, -6, GETUTCDATE()), NULL, DATEADD(DAY, -7, GETUTCDATE()), DATEADD(DAY, -6, GETUTCDATE())),
    (163, 30, 10, 'DEP-2025-000163', 'Cash', 4900.00, 'USD', 24.50, 4875.50, 'Completed', NULL, 'Customer Walk-in', 4, DATEADD(HOUR, 8, DATEADD(DAY, -7, GETUTCDATE())), DATEADD(HOUR, 9, DATEADD(DAY, -7, GETUTCDATE())), DATEADD(DAY, -6, GETUTCDATE()), NULL, DATEADD(DAY, -7, GETUTCDATE()), DATEADD(DAY, -6, GETUTCDATE())),
    (164, 30, 14, 'DEP-2025-000164', 'Cash', 4900.00, 'USD', 24.50, 4875.50, 'Completed', NULL, 'Customer Walk-in', 5, DATEADD(HOUR, 11, DATEADD(DAY, -7, GETUTCDATE())), DATEADD(HOUR, 12, DATEADD(DAY, -7, GETUTCDATE())), DATEADD(DAY, -6, GETUTCDATE()), NULL, DATEADD(DAY, -7, GETUTCDATE()), DATEADD(DAY, -6, GETUTCDATE()));

-- On-hold deposits
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (170, 20, 6, 'DEP-2025-000170', 'BulkCash', 48000.00, 'USD', 30.00, 47970.00, 'OnHold', 'Compliance review required - large amount',                   'Business Representative', NULL, DATEADD(DAY, -12, GETUTCDATE()), NULL, NULL, NULL, DATEADD(DAY, -12, GETUTCDATE()), DATEADD(DAY, -12, GETUTCDATE())),
    (171, 11, 1, 'DEP-2025-000171', 'Cash',     15000.00, 'USD', 30.00, 14970.00, 'OnHold', 'Suspicious activity pattern detected - under investigation',   'Customer Walk-in',       NULL, DATEADD(DAY, -8,  GETUTCDATE()), NULL, NULL, NULL, DATEADD(DAY, -8,  GETUTCDATE()), DATEADD(DAY, -8,  GETUTCDATE())),
    (172, 36, 12,'DEP-2025-000172', 'BulkCash', 35000.00, 'USD', 30.00, 34970.00, 'OnHold', 'Partner bank requested hold pending verification',              'Business Representative', NULL, DATEADD(DAY, -5,  GETUTCDATE()), NULL, NULL, NULL, DATEADD(DAY, -5,  GETUTCDATE()), DATEADD(DAY, -5,  GETUTCDATE()));

-- Pending / Processing deposits
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (175, 3,  2, 'DEP-2025-000175', 'Cash', 1200.00, 'USD',  6.00, 1194.00, 'Pending',    NULL, 'Customer Walk-in', NULL, DATEADD(DAY, -1, GETUTCDATE()), NULL, NULL, NULL, DATEADD(DAY, -1, GETUTCDATE()), DATEADD(DAY, -1, GETUTCDATE())),
    (176, 19, 7, 'DEP-2025-000176', 'Cash', 3500.00, 'USD', 17.50, 3482.50, 'Pending',    NULL, 'Customer Walk-in', NULL, DATEADD(HOUR, -6, GETUTCDATE()), NULL, NULL, NULL, DATEADD(HOUR, -6, GETUTCDATE()), DATEADD(HOUR, -6, GETUTCDATE())),
    (177, 34, 11,'DEP-2025-000177', 'Cash', 2800.00, 'USD', 14.00, 2786.00, 'Processing', NULL, 'Customer Walk-in', 3, DATEADD(HOUR, -3, GETUTCDATE()), DATEADD(HOUR, -2, GETUTCDATE()), NULL, NULL, DATEADD(HOUR, -3, GETUTCDATE()), DATEADD(HOUR, -2, GETUTCDATE()));

-- ANOMALY: Ghost deposits — Completed but ProcessedBy is NULL
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (180, 14, 2,  'DEP-2025-000180', 'Cash', 6200.00, 'USD', 30.00, 6170.00, 'Completed', NULL, 'Customer Walk-in', NULL, DATEADD(DAY, -18, GETUTCDATE()), DATEADD(DAY, -18, GETUTCDATE()), DATEADD(DAY, -17, GETUTCDATE()), NULL, DATEADD(DAY, -18, GETUTCDATE()), DATEADD(DAY, -17, GETUTCDATE())),
    (181, 27, 10, 'DEP-2025-000181', 'Cash', 4300.00, 'USD', 21.50, 4278.50, 'Completed', NULL, 'Customer Walk-in', NULL, DATEADD(DAY, -16, GETUTCDATE()), DATEADD(DAY, -16, GETUTCDATE()), DATEADD(DAY, -15, GETUTCDATE()), NULL, DATEADD(DAY, -16, GETUTCDATE()), DATEADD(DAY, -15, GETUTCDATE())),
    (182, 40, 14, 'DEP-2025-000182', 'Cash', 7100.00, 'USD', 30.00, 7070.00, 'Completed', NULL, 'Customer Walk-in', NULL, DATEADD(DAY, -14, GETUTCDATE()), DATEADD(DAY, -14, GETUTCDATE()), DATEADD(DAY, -13, GETUTCDATE()), NULL, DATEADD(DAY, -14, GETUTCDATE()), DATEADD(DAY, -13, GETUTCDATE()));

-- ANOMALY: Time-travel — SettledDate BEFORE ProcessedDate
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (190, 10, 6, 'DEP-2025-000190', 'Cash', 5500.00, 'USD', 27.50, 5472.50, 'Completed', NULL, 'Customer Walk-in', 4, DATEADD(DAY, -25, GETUTCDATE()), DATEADD(DAY, -24, GETUTCDATE()), DATEADD(DAY, -26, GETUTCDATE()), NULL, DATEADD(DAY, -25, GETUTCDATE()), DATEADD(DAY, -24, GETUTCDATE()));

-- ANOMALY: Zero-fee cash deposits (should always have fees per fee schedule)
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (195, 1,  1,  'DEP-2025-000195', 'Cash', 3000.00, 'USD', 0.00, 3000.00, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(DAY, -28, GETUTCDATE()), DATEADD(DAY, -28, GETUTCDATE()), DATEADD(DAY, -27, GETUTCDATE()), NULL, DATEADD(DAY, -28, GETUTCDATE()), DATEADD(DAY, -27, GETUTCDATE())),
    (196, 6,  3,  'DEP-2025-000196', 'Cash', 4500.00, 'USD', 0.00, 4500.00, 'Completed', NULL, 'Customer Walk-in', 3, DATEADD(DAY, -27, GETUTCDATE()), DATEADD(DAY, -27, GETUTCDATE()), DATEADD(DAY, -26, GETUTCDATE()), NULL, DATEADD(DAY, -27, GETUTCDATE()), DATEADD(DAY, -26, GETUTCDATE())),
    (197, 18, 4,  'DEP-2025-000197', 'Cash', 2200.00, 'USD', 0.00, 2200.00, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(DAY, -26, GETUTCDATE()), DATEADD(DAY, -26, GETUTCDATE()), DATEADD(DAY, -25, GETUTCDATE()), NULL, DATEADD(DAY, -26, GETUTCDATE()), DATEADD(DAY, -25, GETUTCDATE())),
    (198, 35, 8,  'DEP-2025-000198', 'Cash', 6800.00, 'USD', 0.00, 6800.00, 'Completed', NULL, 'Customer Walk-in', 5, DATEADD(DAY, -25, GETUTCDATE()), DATEADD(DAY, -25, GETUTCDATE()), DATEADD(DAY, -24, GETUTCDATE()), NULL, DATEADD(DAY, -25, GETUTCDATE()), DATEADD(DAY, -24, GETUTCDATE()));

-- ANOMALY: Deposits on frozen / suspended accounts
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (200, 42, 10, 'DEP-2025-000200', 'Cash', 3400.00, 'USD', 17.00, 3383.00, 'Completed', NULL, 'Customer Walk-in', 7, DATEADD(DAY, -5, GETUTCDATE()), DATEADD(DAY, -5, GETUTCDATE()), DATEADD(DAY, -4, GETUTCDATE()), NULL, DATEADD(DAY, -5, GETUTCDATE()), DATEADD(DAY, -4, GETUTCDATE())),
    (201, 42, 10, 'DEP-2025-000201', 'Cash', 2100.00, 'USD', 10.50, 2089.50, 'Completed', NULL, 'Customer Walk-in', 7, DATEADD(DAY, -3, GETUTCDATE()), DATEADD(DAY, -3, GETUTCDATE()), DATEADD(DAY, -2, GETUTCDATE()), NULL, DATEADD(DAY, -3, GETUTCDATE()), DATEADD(DAY, -2, GETUTCDATE())),
    (202, 33, 15, 'DEP-2025-000202', 'Cash', 5600.00, 'USD', 28.00, 5572.00, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(DAY, -4, GETUTCDATE()), DATEADD(DAY, -4, GETUTCDATE()), DATEADD(DAY, -3, GETUTCDATE()), NULL, DATEADD(DAY, -4, GETUTCDATE()), DATEADD(DAY, -3, GETUTCDATE())),
    (203, 33, 15, 'DEP-2025-000203', 'Cash', 4800.00, 'USD', 24.00, 4776.00, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(DAY, -2, GETUTCDATE()), DATEADD(DAY, -2, GETUTCDATE()), DATEADD(DAY, -1, GETUTCDATE()), NULL, DATEADD(DAY, -2, GETUTCDATE()), DATEADD(DAY, -1, GETUTCDATE()));

-- ANOMALY: Deposit at Maintenance location
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (205, 21, 9, 'DEP-2025-000205', 'Cash', 1900.00, 'USD', 9.50, 1890.50, 'Completed', NULL, 'Customer Walk-in', 3, DATEADD(DAY, -6, GETUTCDATE()), DATEADD(DAY, -6, GETUTCDATE()), DATEADD(DAY, -5, GETUTCDATE()), NULL, DATEADD(DAY, -6, GETUTCDATE()), DATEADD(DAY, -5, GETUTCDATE()));

-- ANOMALY: Possible duplicate entries
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (206, 46, 1, 'DEP-2025-000206', 'Cash', 7250.00, 'USD', 30.00, 7220.00, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(DAY, -3, GETUTCDATE()), DATEADD(HOUR, 1, DATEADD(DAY, -3, GETUTCDATE())), DATEADD(DAY, -2, GETUTCDATE()), NULL, DATEADD(DAY, -3, GETUTCDATE()), DATEADD(DAY, -2, GETUTCDATE())),
    (207, 46, 1, 'DEP-2025-000207', 'Cash', 7250.00, 'USD', 30.00, 7220.00, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(MINUTE, 3, DATEADD(DAY, -3, GETUTCDATE())), DATEADD(HOUR, 1, DATEADD(DAY, -3, GETUTCDATE())), DATEADD(DAY, -2, GETUTCDATE()), NULL, DATEADD(DAY, -3, GETUTCDATE()), DATEADD(DAY, -2, GETUTCDATE()));

-- ANOMALY: Deposits against suspended partner (ACB)
INSERT INTO Deposits (DepositID, AccountID, LocationID, ReferenceNumber, DepositType, Amount, Currency, FeeAmount, NetAmount, Status, StatusReason, DepositedBy, ProcessedBy, DepositDate, ProcessedDate, SettledDate, Notes, CreatedAt, UpdatedAt)
VALUES
    (210, 38, 14, 'DEP-2025-000210', 'Cash', 2300.00, 'USD', 13.80, 2286.20, 'Completed', NULL, 'Customer Walk-in', 7, DATEADD(DAY, -9, GETUTCDATE()), DATEADD(DAY, -9, GETUTCDATE()), DATEADD(DAY, -8, GETUTCDATE()), NULL, DATEADD(DAY, -9, GETUTCDATE()), DATEADD(DAY, -8, GETUTCDATE())),
    (211, 38, 14, 'DEP-2025-000211', 'Cash', 1600.00, 'USD',  9.60, 1590.40, 'Completed', NULL, 'Customer Walk-in', 7, DATEADD(DAY, -6, GETUTCDATE()), DATEADD(DAY, -6, GETUTCDATE()), DATEADD(DAY, -5, GETUTCDATE()), NULL, DATEADD(DAY, -6, GETUTCDATE()), DATEADD(DAY, -5, GETUTCDATE())),
    (212, 39, 15, 'DEP-2025-000212', 'Cash', 3100.00, 'USD', 18.60, 3081.40, 'Completed', NULL, 'Customer Walk-in', 2, DATEADD(DAY, -4, GETUTCDATE()), DATEADD(DAY, -4, GETUTCDATE()), DATEADD(DAY, -3, GETUTCDATE()), NULL, DATEADD(DAY, -4, GETUTCDATE()), DATEADD(DAY, -3, GETUTCDATE()));

SET IDENTITY_INSERT Deposits OFF;
GO

-- =====================================================================================
-- Reconciliation - Daily reconciliation records with discrepancies
-- =====================================================================================
SET IDENTITY_INSERT Reconciliation ON;

DECLARE @r INT = 1;
DECLARE @rDate DATE = DATEADD(DAY, -30, CAST(GETUTCDATE() AS DATE));

WHILE @r <= 42
BEGIN
    DECLARE @rPartner INT = ((@r - 1) % 7) + 1;
    DECLARE @pCount INT = 5 + ((@r * 7) % 20);
    DECLARE @pTotal DECIMAL(18,2) = CAST(@pCount * (1200 + (@r * 37) % 2000) AS DECIMAL(18,2));
    DECLARE @pFees DECIMAL(18,2) = CAST(@pTotal * 0.005 AS DECIMAL(18,2));

    DECLARE @partCount INT;
    DECLARE @partTotal DECIMAL(18,2);
    DECLARE @partFees DECIMAL(18,2);
    DECLARE @rStatus NVARCHAR(20);
    DECLARE @cDiscrep INT;
    DECLARE @aDiscrep DECIMAL(18,2);
    DECLARE @rBy INT = NULL;
    DECLARE @rAt DATETIME2 = NULL;
    DECLARE @rNotes NVARCHAR(1000) = NULL;

    IF @r IN (5, 12, 23, 31, 38)
    BEGIN
        SET @partCount = @pCount + CASE WHEN @r IN (5, 23) THEN -2 ELSE 1 END;
        SET @partTotal = @pTotal + CAST(100 + (@r * 53) % 900 AS DECIMAL(18,2));
        SET @partFees = CAST(@partTotal * 0.005 AS DECIMAL(18,2));
        SET @cDiscrep = @partCount - @pCount;
        SET @aDiscrep = @partTotal - @pTotal;

        IF @r IN (12, 38)
        BEGIN
            SET @rStatus = 'Resolved';
            SET @rBy = 4;
            SET @rAt = DATEADD(HOUR, 10, CAST(DATEADD(DAY, @r % 30, @rDate) AS DATETIME2));
            SET @rNotes = 'Discrepancy resolved - missing deposits were delayed and processed next day';
        END
        ELSE IF @r = 31
        BEGIN
            SET @rStatus = 'Escalated';
            SET @rBy = 4;
            SET @rAt = DATEADD(HOUR, 12, CAST(DATEADD(DAY, @r % 30, @rDate) AS DATETIME2));
            SET @rNotes = 'Large discrepancy escalated to partner bank and management. Partner reported system outage during settlement window.';
        END
        ELSE
        BEGIN
            SET @rStatus = 'Discrepancy';
            SET @rNotes = 'Count mismatch: ' + CAST(@cDiscrep AS NVARCHAR) + ' transactions. Amount discrepancy: $' + CAST(@aDiscrep AS NVARCHAR) + '. Pending investigation.';
        END;
    END
    ELSE IF @r IN (8, 17, 28, 40)
    BEGIN
        SET @partCount = NULL;
        SET @partTotal = NULL;
        SET @partFees = NULL;
        SET @cDiscrep = 0;
        SET @aDiscrep = 0.00;
        SET @rStatus = 'Pending';
        SET @rNotes = 'Awaiting partner bank reconciliation file';
    END
    ELSE
    BEGIN
        SET @partCount = @pCount;
        SET @partTotal = @pTotal;
        SET @partFees = @pFees;
        SET @cDiscrep = 0;
        SET @aDiscrep = 0.00;
        SET @rStatus = 'Matched';
        SET @rBy = 4;
        SET @rAt = DATEADD(HOUR, 8, CAST(DATEADD(DAY, @r % 30, @rDate) AS DATETIME2));
    END;

    INSERT INTO Reconciliation (ReconciliationID, PartnerID, ReconciliationDate, PlatformDepositCount, PlatformDepositTotal, PlatformFeeTotal, PartnerDepositCount, PartnerDepositTotal, PartnerFeeTotal, CountDiscrepancy, AmountDiscrepancy, Status, ReconciledBy, ReconciledAt, Notes, CreatedAt)
    VALUES (
        @r, @rPartner,
        DATEADD(DAY, @r % 30, @rDate),
        @pCount, @pTotal, @pFees,
        @partCount, @partTotal, @partFees,
        @cDiscrep, @aDiscrep,
        @rStatus, @rBy, @rAt, @rNotes,
        DATEADD(HOUR, 1, CAST(DATEADD(DAY, @r % 30, @rDate) AS DATETIME2))
    );

    SET @r = @r + 1;
END;

SET IDENTITY_INSERT Reconciliation OFF;
GO

-- =====================================================================================
-- AuditLog - System audit trail entries
-- =====================================================================================
SET IDENTITY_INSERT AuditLog ON;

INSERT INTO AuditLog (AuditID, UserID, Action, EntityType, EntityID, OldValue, NewValue, IPAddress, UserAgent, Timestamp)
VALUES
    ( 1, 1, 'Login',                   'User',           '1',   NULL,                       NULL,                                                                   '192.168.1.100', 'Mozilla/5.0', DATEADD(HOUR, -2, GETUTCDATE())),
    ( 2, 2, 'Login',                   'User',           '2',   NULL,                       NULL,                                                                   '192.168.1.101', 'Mozilla/5.0', DATEADD(HOUR, -5, GETUTCDATE())),
    ( 3, 2, 'ProcessDeposit',          'Deposit',        '150', '{"Status":"Pending"}',     '{"Status":"Processing"}',                                              '192.168.1.101', 'Mozilla/5.0', DATEADD(HOUR, -4, GETUTCDATE())),
    ( 4, 2, 'ProcessDeposit',          'Deposit',        '150', '{"Status":"Processing"}',  '{"Status":"Completed"}',                                               '192.168.1.101', 'Mozilla/5.0', DATEADD(HOUR, -4, GETUTCDATE())),
    ( 5, 3, 'Login',                   'User',           '3',   NULL,                       NULL,                                                                   '192.168.1.102', 'Mozilla/5.0', DATEADD(HOUR, -3, GETUTCDATE())),
    ( 6, 3, 'ViewAccount',             'Account',        '25',  NULL,                       NULL,                                                                   '192.168.1.102', 'Mozilla/5.0', DATEADD(HOUR, -3, GETUTCDATE())),
    ( 7, 4, 'Login',                   'User',           '4',   NULL,                       NULL,                                                                   '192.168.1.103', 'Mozilla/5.0', DATEADD(HOUR, -8, GETUTCDATE())),
    ( 8, 4, 'ReconcilePartner',        'Reconciliation', '12',  '{"Status":"Discrepancy"}', '{"Status":"Resolved"}',                                                '192.168.1.103', 'Mozilla/5.0', DATEADD(DAY, -5, GETUTCDATE())),
    ( 9, 6, 'SuspendAccount',          'Account',        '15',  '{"Status":"Active"}',      '{"Status":"Suspended","Reason":"Suspicious activity pattern"}',        '192.168.1.104', 'Mozilla/5.0', DATEADD(DAY, -7, GETUTCDATE())),
    (10, 2, 'ReverseDeposit',          'Deposit',        '146', '{"Status":"Completed"}',   '{"Status":"Reversed","Reason":"Customer request"}',                    '192.168.1.101', 'Mozilla/5.0', DATEADD(DAY, -10, GETUTCDATE())),
    (11, 1, 'UpdatePartner',           'Partner',        '6',   '{"Status":"Active"}',      '{"Status":"Suspended","Reason":"Compliance review"}',                  '192.168.1.100', 'Mozilla/5.0', DATEADD(DAY, -30, GETUTCDATE())),
    (12, 5, 'Login',                   'User',           '5',   NULL,                       NULL,                                                                   '192.168.1.105', 'Mozilla/5.0', DATEADD(DAY, -1, GETUTCDATE())),
    (13, 5, 'ExportAuditReport',       'AuditLog',       NULL,  NULL,                       '{"DateRange":"2025-01-01 to 2025-02-01","PartnerID":"6"}',             '192.168.1.105', 'Mozilla/5.0', DATEADD(DAY, -1, GETUTCDATE())),
    (14, 2, 'ProcessDeposit',          'Deposit',        '142', '{"Status":"Pending"}',     '{"Status":"Failed","Reason":"Partner API timeout"}',                   '192.168.1.101', 'Mozilla/5.0', DATEADD(HOUR, -12, GETUTCDATE())),
    (15, 6, 'EscalateReconciliation',  'Reconciliation', '31',  '{"Status":"Discrepancy"}', '{"Status":"Escalated"}',                                               '192.168.1.104', 'Mozilla/5.0', DATEADD(DAY, -3, GETUTCDATE())),
    (16, 3, 'ViewDeposit',             'Deposit',        '150', NULL,                       NULL,                                                                   '192.168.1.102', 'Mozilla/5.0', DATEADD(HOUR, -2, GETUTCDATE())),
    (17, 3, 'ViewDeposit',             'Deposit',        '151', NULL,                       NULL,                                                                   '192.168.1.102', 'Mozilla/5.0', DATEADD(HOUR, -2, GETUTCDATE())),
    (18, 3, 'ViewDeposit',             'Deposit',        '152', NULL,                       NULL,                                                                   '192.168.1.102', 'Mozilla/5.0', DATEADD(HOUR, -2, GETUTCDATE())),
    (19, 3, 'ViewDeposit',             'Deposit',        '153', NULL,                       NULL,                                                                   '192.168.1.102', 'Mozilla/5.0', DATEADD(HOUR, -2, GETUTCDATE())),
    (20, 7, 'Login',                   'User',           '7',   NULL,                       NULL,                                                                   '192.168.1.106', 'Mozilla/5.0', DATEADD(DAY, -2, GETUTCDATE())),
    (21, 2, 'HoldDeposit',             'Deposit',        '171', '{"Status":"Pending"}',     '{"Status":"OnHold","Reason":"Compliance review required"}',            '192.168.1.101', 'Mozilla/5.0', DATEADD(DAY, -15, GETUTCDATE())),
    (22, 1, 'CreateUser',              'User',           '8',   NULL,                       '{"Username":"tmiller","Role":"Support"}',                               '192.168.1.100', 'Mozilla/5.0', DATEADD(DAY, -180, GETUTCDATE())),
    (23, 1, 'DeactivateUser',          'User',           '8',   '{"Status":"Active"}',      '{"Status":"Inactive"}',                                                '192.168.1.100', 'Mozilla/5.0', DATEADD(DAY, -50, GETUTCDATE())),
    (24, 6, 'FreezeAccount',           'Account',        '42',  '{"Status":"Active"}',      '{"Status":"Frozen","Reason":"Fraud investigation"}',                   '192.168.1.104', 'Mozilla/5.0', DATEADD(DAY, -20, GETUTCDATE())),
    (25, 4, 'ReconcilePartner',        'Reconciliation', '38',  '{"Status":"Discrepancy"}', '{"Status":"Resolved"}',                                                '192.168.1.103', 'Mozilla/5.0', DATEADD(DAY, -8, GETUTCDATE()));

SET IDENTITY_INSERT AuditLog OFF;
GO

-- =====================================================================================
-- Summary
-- =====================================================================================
PRINT '';
PRINT '==========================================================================';
PRINT ' Database Seeding Complete!';
PRINT '==========================================================================';
PRINT '';
PRINT ' Tables: Partners, Users, DepositLocations, Accounts, Fees,';
PRINT '         Deposits, Reconciliation, AuditLog';
PRINT '';
PRINT ' Data anomalies to discover with the query tool:';
PRINT ' ------------------------------------------------';
PRINT '  1. Structuring (smurfing): 4 deposits ~$9.5-9.8k on Account 25,';
PRINT '     same day, same location (Deposits 150-153)';
PRINT '  2. Velocity abuse: 5 x $4,900 across 5 cities on Account 30';
PRINT '     in one day (Deposits 160-164)';
PRINT '  3. Ghost deposits: 3 Completed deposits with no ProcessedBy';
PRINT '     operator (Deposits 180-182)';
PRINT '  4. Time-travel: Deposit 190 settled BEFORE it was processed';
PRINT '  5. Zero-fee cash deposits: Deposits 195-198 charged $0 fee';
PRINT '  6. Frozen/Suspended account deposits: Completed deposits on';
PRINT '     Account 42 (Frozen) and Account 33 (Suspended)';
PRINT '  7. Deposits against Suspended partner (ACB): Deposits 210-212';
PRINT '  8. Deposit at Maintenance location: Deposit 205 at Location 9';
PRINT '  9. Possible duplicate: Deposits 206 & 207 same account,';
PRINT '     location, amount, 3 minutes apart';
PRINT ' 10. Negative fee rate: Partner 4 (PSB) FeePercentage = -0.20%%';
PRINT ' 11. Min > Max fee: Fee ID 9 MinFee=$150, MaxFee=$15';
PRINT ' 12. Negative account balance: Account 42 = -$1,250';
PRINT ' 13. Currency mismatch: Account 22 is GBP, deposits are USD';
PRINT ' 14. Inactive user login: User 8 Inactive but logged in 3d ago';
PRINT ' 15. Zero max deposit limit: Location 20 (Seattle) = $0 limit';
PRINT ' 16. Stale KYC: Account 47 RequiresUpdate for 300+ days';
PRINT ' 17. Pending KYC with deposits: Accounts 48-50';
PRINT ' 18. Active accounts on Suspended partner: Accounts 38-39';
PRINT '     belong to Partner 6 (ACB) which is Suspended';
PRINT '';
PRINT '==========================================================================';
GO