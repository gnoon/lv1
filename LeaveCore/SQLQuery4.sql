ALTER TABLE [dbo].[HR Employee] DROP CONSTRAINT [FK_Employee_Person]
GO
/****** Object:  Table [dbo].[HR Employee]    Script Date: 10/9/2013 15:48:31 ******/
DROP TABLE [dbo].[HR Employee]
GO
/****** Object:  Table [dbo].[HR Employee]    Script Date: 10/9/2013 15:48:31 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_PADDING ON
GO
CREATE TABLE [dbo].[HR Employee](
	[Employee No] [varchar](20) NOT NULL,
	[Person No] [varchar](80) NOT NULL,
	[Starting Date] [datetime] NOT NULL,
	[Until Date] [datetime] NOT NULL,
	[Company Code] [varchar](40) NOT NULL,
	[Department] [varchar](40) NOT NULL,
	[Section] [varchar](40) NOT NULL,
 CONSTRAINT [PK_HR Employee] PRIMARY KEY CLUSTERED 
(
	[Employee No] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_PADDING OFF
GO
INSERT [dbo].[HR Employee] ([Employee No], [Person No], [Starting Date], [Until Date], [Company Code], [Department], [Section]) VALUES (N'G-0230', N'sayamdans', CAST(0x0000A01500000000 AS DateTime), CAST(0x002D247F00000000 AS DateTime), N'Verasu Group Co., Ltd', N'BD', N'APS')
GO
INSERT [dbo].[HR Employee] ([Employee No], [Person No], [Starting Date], [Until Date], [Company Code], [Department], [Section]) VALUES (N'G-0235', N'nopadols', CAST(0x0000A0A000000000 AS DateTime), CAST(0x002D247F00000000 AS DateTime), N'Verasu Group Co., Ltd', N'BD', N'APS')
GO
INSERT [dbo].[HR Employee] ([Employee No], [Person No], [Starting Date], [Until Date], [Company Code], [Department], [Section]) VALUES (N'G-0249', N'anusorns', CAST(0x0000A11C00000000 AS DateTime), CAST(0x002D247F00000000 AS DateTime), N'Verasu Group Co., Ltd', N'BD', N'APS')
GO
INSERT [dbo].[HR Employee] ([Employee No], [Person No], [Starting Date], [Until Date], [Company Code], [Department], [Section]) VALUES (N'G-0260', N'rungaroonb', CAST(0x0000A1FD00000000 AS DateTime), CAST(0x002D247F00000000 AS DateTime), N'Verasu Group Co., Ltd', N'BD', N'APS')
GO
ALTER TABLE [dbo].[HR Employee]  WITH NOCHECK ADD  CONSTRAINT [FK_Employee_Person] FOREIGN KEY([Person No])
REFERENCES [dbo].[HR Person] ([Person No])
NOT FOR REPLICATION 
GO
ALTER TABLE [dbo].[HR Employee] NOCHECK CONSTRAINT [FK_Employee_Person]
GO
