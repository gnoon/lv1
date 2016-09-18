/****** Object:  Trigger [dbo].[NO_DATES_CONFLICT]    Script Date: 10/9/2013 17:18:40 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE TRIGGER [dbo].[NO_DATES_CONFLICT]
   ON  [dbo].[HR Employee]
   AFTER INSERT,UPDATE
AS 
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	-- Prevent from recursion
	IF (SELECT TRIGGER_NESTLEVEL()) > 2 RETURN;

	IF EXISTS(SELECT 1 FROM [HR Employee] a
		INNER JOIN Inserted b ON a.[Person No]=b.[Person No] AND a.[Employee No]=b.[Employee No]
		AND (a.[Starting Date]<>b.[Starting Date] OR a.[Until Date]<>b.[Until Date])
		WHERE (a.[Starting Date]<b.[Until Date] AND a.[Starting Date] BETWEEN b.[Starting Date] AND b.[Until Date])
		OR (a.[Until Date]>b.[Starting Date] AND a.[Until Date] BETWEEN b.[Starting Date] AND b.[Until Date])
		OR (b.[Starting Date]>=a.[Starting Date] AND a.[Until Date]>=b.[Until Date])
	)
	BEGIN
		RAISERROR('Date range conflicts with existing record', 11, 1);
		RETURN;
	END
END

GO


