/****** Script for SelectTopNRows command from SSMS  ******/
update [LV Workshifts] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update [LV Workshift] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update a set [Head Person No]=b.[Person No]
FROM [LV Veto] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update a set [Person No]=b.[Phone No]
FROM [LV Request] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update a set [Person No]=b.[Phone No]
FROM [LV Quota] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update [LV Quota] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update a set [Person No]=b.[Phone No]
FROM [LV Profile Workshift] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update [LV Profile Workshift] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update a set [Person No]=b.[Phone No]
FROM [LV Profile Weekend Movement] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update [LV Profile Weekend Movement] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update a set [Person No]=b.[Phone No]
FROM [LV Profile Weekend] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update [LV Profile Weekend] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update a set a.[Person No]=b.[Phone No]
FROM [LV Profile Veto] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update [LV Profile Veto] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update a set [Person No]=b.[Phone No]
FROM [LV Profile Holiday] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update [LV Profile Holiday] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update a set [Person No]=b.[Phone No]
FROM [LV Profile Grantor] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update [LV Profile Grantor] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update a set [Person No]=b.[Phone No]
FROM [LV Profile] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update [LV Period] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update a set [Person No]=b.[Phone No]
FROM [LV Leave] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update [LV Leave] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update [LV Holidays] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update [LV Holiday] set [Modify Person]='FD2ED2290BCBFFC486D1D1B121510D78'

update a set [Head Person No]=b.[Phone No]
FROM [LV Grant] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update a set [Person No]=b.[Phone No]
FROM [HR Employee] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]

update a set [Person No]=b.[Phone No]
FROM [HR Person] a
inner join [Raw] _ on _.[Person No]=a.[Head Person No]
inner join [HR Person] b on b.[Person No]=_.[Person No]