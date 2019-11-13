
-- --------------------------------------------------
-- Entity Designer DDL Script for SQL Server 2005, 2008, 2012 and Azure
-- --------------------------------------------------
-- Date Created: 11/13/2019 11:48:13
-- Generated from EDMX file: D:\Coding\ConsoleApp\ParallelRecognition\RecognitionModel.edmx
-- --------------------------------------------------

SET QUOTED_IDENTIFIER OFF;
GO
USE [Recognition];
GO
IF SCHEMA_ID(N'dbo') IS NULL EXECUTE(N'CREATE SCHEMA [dbo]');
GO

-- --------------------------------------------------
-- Dropping existing FOREIGN KEY constraints
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[FK_ResultsBlobs]', 'F') IS NOT NULL
    ALTER TABLE [dbo].[Results] DROP CONSTRAINT [FK_ResultsBlobs];
GO

-- --------------------------------------------------
-- Dropping existing tables
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[Results]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Results];
GO
IF OBJECT_ID(N'[dbo].[Blobs]', 'U') IS NOT NULL
    DROP TABLE [dbo].[Blobs];
GO

-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'Results'
CREATE TABLE [dbo].[Results] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [ClassId] int  NOT NULL,
    [FileHash] bigint  NOT NULL,
    [Probability] float  NOT NULL,
    [HitCount] int  NOT NULL,
    [Blob_Id] int  NOT NULL
);
GO

-- Creating table 'Blobs'
CREATE TABLE [dbo].[Blobs] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [FileContent] varbinary(max)  NOT NULL
);
GO

-- --------------------------------------------------
-- Creating all PRIMARY KEY constraints
-- --------------------------------------------------

-- Creating primary key on [Id] in table 'Results'
ALTER TABLE [dbo].[Results]
ADD CONSTRAINT [PK_Results]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- Creating primary key on [Id] in table 'Blobs'
ALTER TABLE [dbo].[Blobs]
ADD CONSTRAINT [PK_Blobs]
    PRIMARY KEY CLUSTERED ([Id] ASC);
GO

-- --------------------------------------------------
-- Creating all FOREIGN KEY constraints
-- --------------------------------------------------

-- Creating foreign key on [Blob_Id] in table 'Results'
ALTER TABLE [dbo].[Results]
ADD CONSTRAINT [FK_ResultsBlobs]
    FOREIGN KEY ([Blob_Id])
    REFERENCES [dbo].[Blobs]
        ([Id])
    ON DELETE NO ACTION ON UPDATE NO ACTION;
GO

-- Creating non-clustered index for FOREIGN KEY 'FK_ResultsBlobs'
CREATE INDEX [IX_FK_ResultsBlobs]
ON [dbo].[Results]
    ([Blob_Id]);
GO

-- --------------------------------------------------
-- Script has ended
-- --------------------------------------------------