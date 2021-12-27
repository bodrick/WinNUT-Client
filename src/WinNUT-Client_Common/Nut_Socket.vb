' WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
' Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
'
' This program is free software: you can redistribute it and/or modify it under the terms of the
' GNU General Public License as published by the Free Software Foundation, either version 3 of the
' License, or any later version.
'
' This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

' Class dealing only with the management of the communication socket with the Nut server
Imports System.IO
Imports System.Net.Sockets
Imports System.Windows.Forms

Public Class Nut_Socket
    Public Auth_Success As Boolean = False

    Private ReadOnly Nut_Parameter As Nut_Parameter

    Private ReadOnly WatchDog As New Timer

    Private ConnectionStatus As Boolean = False

    Private Net_Ver As String

    Private Nut_Config As Nut_Parameter

    Private Nut_Ver As String

    'Socket Variables
    Private NutSocket As Socket

    Private NutStream As NetworkStream
    Private NutTCP As TcpClient
    Private ReaderStream As StreamReader
    Private WriterStream As StreamWriter

    Public Sub New(Nut_Config As Nut_Parameter)

        With WatchDog
            .Interval = 1000
            .Enabled = False
            AddHandler .Tick, AddressOf Event_WatchDog
        End With
        Update_Config(Nut_Config)
    End Sub

    'Public Event OnError(Excep As Exception, NoticeLvl As LogLvl, sender As Object, ReportToGui As Boolean)
    Public Event OnError(Excep As Exception, NoticeLvl As LogLvl, sender As Object)

    'Public Event OnNotice(Message As String, NoticeLvl As LogLvl, sender As Object, ReportToGui As Boolean)
    Public Event OnNotice(Message As String, NoticeLvl As LogLvl, sender As Object)

    Public Event Socket_Broken()

    Public Event Socket_Deconnected()

    Public Event Unknown_UPS()

    Public ReadOnly Property IsConnected() As Boolean
        Get
            Return ConnectionStatus
        End Get
    End Property

    Public ReadOnly Property Net_Version() As String
        Get
            Return Net_Ver
        End Get
    End Property

    Public ReadOnly Property Nut_Version() As String
        Get
            Return Nut_Ver
        End Get
    End Property

    Public ReadOnly Property IsKnownUPS(Test_UPSname As String) As Boolean
        Get
            If Not ConnectionStatus Then
                Return False
            Else
                Dim IsKnow As Boolean = False
                Dim ListOfUPSs = Query_List_Datas("LIST UPS")
                For Each Known_UPS In ListOfUPSs
                    If Known_UPS.VarValue = Test_UPSname Then
                        IsKnow = True
                    End If
                Next
                Return IsKnow
            End If
        End Get
    End Property

    Public Function Connect() As Boolean
        Try
            'TODO: Use LIST UPS protocol command to get valid UPSs.
            Dim Host = Nut_Config.Host
            Dim Port = Nut_Config.Port
            Dim Login = Nut_Config.Login
            Dim Password = Nut_Config.Password
            Dim Response As Boolean = False
            If Not String.IsNullOrEmpty(Host) And Not IsNothing(Port) Then
                If Not Create_Socket(Host, Port) Then
                    Throw New Nut_Exception(Nut_Exception_Value.CONNECT_ERROR)
                    Disconnect()
                Else
                    If Not AuthLogin(Login, Password) Then
                        Throw New Nut_Exception(Nut_Exception_Value.INVALID_AUTH_DATA)
                    End If
                    Dim Nut_Query = Query_Data("VER")

                    If Nut_Query.Response = NUTResponse.OK Then
                        Nut_Ver = (Nut_Query.Data.Split(" "c))(4)
                    End If
                    Nut_Query = Query_Data("NETVER")

                    If Nut_Query.Response = NUTResponse.OK Then
                        Net_Ver = Nut_Query.Data
                    End If
                    Response = True
                End If
            End If
            Return Response
        Catch Excep As Exception
            RaiseEvent OnError(Excep, LogLvl.LOG_ERROR, Me)
            Return False
        End Try
    End Function

    Public Sub Disconnect(Optional ForceDisconnect = False)
        Query_Data("LOGOUT")
        Close_Socket()
        WatchDog.Stop()
        If Not ForceDisconnect Then
            RaiseEvent Socket_Deconnected()
        End If
    End Sub

    Public Function Query_Data(Query_Msg As String) As (Data As String, Response As NUTResponse)
        Dim Response As NUTResponse = NUTResponse.NORESPONSE
        Dim DataResult As String = ""
        Try
            If ConnectionStatus Then
                WriterStream.WriteLine(Query_Msg & vbCr)
                WriterStream.Flush()
                DataResult = Trim(ReaderStream.ReadLine())

                If DataResult = Nothing Then
                    Disconnect()
                    RaiseEvent Socket_Broken()
                    Throw New Nut_Exception(Nut_Exception_Value.SOCKET_BROKEN, Query_Msg)
                End If
                Response = EnumResponse(DataResult)
            End If
            Return (DataResult, Response)
        Catch Excep As Exception
            RaiseEvent OnError(Excep, LogLvl.LOG_ERROR, Me)
            Return (DataResult, Response)
        End Try
    End Function

    Public Function Query_Desc(VarName As String) As String
        Try
            If Not ConnectionStatus Then
                Disconnect()
                RaiseEvent Socket_Broken()
                Throw New Nut_Exception(Nut_Exception_Value.SOCKET_BROKEN, VarName)
            Else
                Dim Nut_Query = Query_Data("GET DESC " & Nut_Config.UPSName & " " & VarName)
                Select Case Nut_Query.Response
                    Case NUTResponse.OK
                        'LogFile.LogTracing("Process Result With " & VarName & " : " & Nut_Query.Data, LogLvl.LOG_DEBUG, Me)
                        Return Nut_Query.Data
                    Case NUTResponse.UNKNOWNUPS
                        RaiseEvent Unknown_UPS()
                        Return Nothing
                    Case Else
                        'LogFile.LogTracing("Error Result On Retrieving  " & VarName & " : " & Nut_Query.Data, LogLvl.LOG_ERROR, Me)
                        Return Nothing
                End Select
            End If
        Catch Excep As Exception
            RaiseEvent OnError(Excep, LogLvl.LOG_ERROR, Me)
            Return Nothing
        End Try
    End Function

    Public Function Query_List_Datas(Query_Msg As String) As List(Of UPS_List_Datas)
        Dim List_Datas As New List(Of String)
        Dim List_Result As New List(Of UPS_List_Datas)
        Try
            If ConnectionStatus Then
                WriterStream.WriteLine(Query_Msg & vbCr)
                WriterStream.Flush()
                Threading.Thread.Sleep(100)
                Dim DataResult As String = ""
                Dim start As DateTime = DateTime.Now
                Do
                    DataResult = ReaderStream.ReadLine()
                    List_Datas.Add(DataResult)
                Loop Until (IsNothing(DataResult) Or (ReaderStream.Peek < 0))

                If (EnumResponse(List_Datas(0)) <> NUTResponse.OK) Or (List_Datas.Count = 0) Then
                    Throw New Nut_Exception(Nut_Exception_Value.SOCKET_BROKEN, Query_Msg)
                End If

                Dim Key As String
                Dim Value As String
                For Each Line In List_Datas
                    Dim SplitString = Split(Line, " ", 4)

                    Select Case SplitString(0)
                        Case "BEGIN"
                        Case "VAR"
                            'Query
                            'LIST VAR <upsname>
                            'Response List of var
                            'VAR <upsname> <varname> "<value>"
                            Key = Strings.Replace(SplitString(2), """", "")
                            Value = Strings.Replace(SplitString(3), """", "")
                            Dim UPSName = SplitString(1)
                            Dim VarDESC = Query_Desc(Key)
                            If Not IsNothing(VarDESC) Then
                                List_Result.Add(New UPS_List_Datas With {
                                    .VarKey = Key,
                                    .VarValue = Trim(Value),
                                    .VarDesc = Split(Strings.Replace(VarDESC, """", ""), " ", 4)(3)}
                                )
                            Else
                                'TODO: Convert to nut_exception error
                                Throw New Exception("error")
                            End If
                        Case "UPS"
                            'Query
                            'LIST UPS
                            'List of ups
                            'UPS <upsname> "<description>"
                            List_Result.Add(New UPS_List_Datas With {
                                    .VarKey = "UPSNAME",
                                    .VarValue = SplitString(1),
                                    .VarDesc = Strings.Replace(SplitString(2), """", "")}
                                )
                        Case "RW"
                            'Query
                            'LIST RW <upsname>
                            'List of RW var
                            'RW <upsname> <varname> "<value>"
                            Key = Strings.Replace(SplitString(2), """", "")
                            Value = Strings.Replace(SplitString(3), """", "")
                            Dim UPSName = SplitString(1)
                            Dim VarDESC = Query_Desc(Key)
                            If Not IsNothing(VarDESC) Then
                                List_Result.Add(New UPS_List_Datas With {
                                    .VarKey = Key,
                                    .VarValue = Trim(Value),
                                    .VarDesc = Split(Strings.Replace(VarDESC, """", ""), " ", 4)(3)}
                                )
                            Else
                                'TODO: Convert to nut_exception error
                                Throw New Exception("error")
                            End If
                        Case "CMD"
                            'Query
                            'LIST CMD <upsname>
                            'List of CMD
                            'CMD <upsname> <cmdname>
                        Case "ENUM"
                            'Query
                            'LIST ENUM <upsname>
                            'List of Enum ??
                            'ENUM <upsname> <varname> "<value>"
                            Key = Strings.Replace(SplitString(2), """", "")
                            Value = Strings.Replace(SplitString(3), """", "")
                            Dim UPSName = SplitString(1)
                            Dim VarDESC = Query_Data("GET DESC " & UPSName & " " & Key)
                            If VarDESC.Response = NUTResponse.OK Then
                                List_Result.Add(New UPS_List_Datas With {
                                    .VarKey = Key,
                                    .VarValue = Value,
                                    .VarDesc = Split(Strings.Replace(VarDESC.Data, """", ""), " ", 4)(3)}
                                )
                            Else
                                'TODO: Convert to nut_exception error
                                Throw New Exception("error")
                            End If
                        Case "RANGE"
                            'Query
                            'LIST RANGE <upsname> <varname>
                            'List of Range
                            'RANGE <upsname> <varname> "<min>" "<max>"
                        Case "CLIENT"
                            'Query
                            'LIST CLIENT <upsname>
                            'List of Range
                            'CLIENT <device name> <client IP address>
                    End Select
                Next
            End If
            Return List_Result
        Catch Excep As Exception
            RaiseEvent OnError(Excep, LogLvl.LOG_ERROR, Me)
            Disconnect()
            Return Nothing
        End Try
    End Function

    Public Sub Update_Config(New_Nut_Config As Nut_Parameter)
        Nut_Config = New_Nut_Config
    End Sub

    Private Function AuthLogin(Login As String, Password As String) As Boolean
        Try
            Auth_Success = False
            If Not String.IsNullOrEmpty(Login) AndAlso String.IsNullOrEmpty(Password) Then
                Dim Nut_Query = Query_Data("USERNAME " & Login)

                If Nut_Query.Response <> NUTResponse.OK Then
                    If Nut_Query.Response = NUTResponse.INVALIDUSERNAME Then
                        Throw New Nut_Exception(Nut_Exception_Value.INVALID_USERNAME)
                    ElseIf Nut_Query.Response = NUTResponse.ACCESSDENIED Then
                        Throw New Nut_Exception(Nut_Exception_Value.ACCESS_DENIED)
                    Else
                        Throw New Nut_Exception(Nut_Exception_Value.UNKNOWN_LOGIN_ERROR, Nut_Query.Data)
                    End If
                End If

                Nut_Query = Query_Data("PASSWORD " & Password)

                If Nut_Query.Response <> NUTResponse.OK Then
                    If Nut_Query.Response = NUTResponse.INVALIDPASSWORD Then
                        Throw New Nut_Exception(Nut_Exception_Value.INVALID_PASSWORD)
                    ElseIf Nut_Query.Response = NUTResponse.ACCESSDENIED Then
                        Throw New Nut_Exception(Nut_Exception_Value.ACCESS_DENIED)
                    Else
                        Throw New Nut_Exception(Nut_Exception_Value.UNKNOWN_LOGIN_ERROR, Nut_Query.Data)
                    End If
                End If
            End If
            Auth_Success = True
            Return Auth_Success
        Catch Excep As Exception
            RaiseEvent OnError(Excep, LogLvl.LOG_ERROR, Me)
            Disconnect()
            Return False
        End Try
    End Function

    Private Sub Close_Socket()
        Try
            WriterStream.Close()
            ReaderStream.Close()
            NutStream.Close()
            NutTCP.Close()
            NutSocket.Close()
        Catch Excep As Exception
        End Try
        ConnectionStatus = False
    End Sub

    Private Function Create_Socket(Host As String, Port As Integer) As Boolean
        Try
            NutSocket = New Socket(AddressFamily.InterNetwork, ProtocolType.IP)
            NutTCP = New TcpClient(Host, Port)
            NutStream = NutTCP.GetStream
            ReaderStream = New StreamReader(NutStream)
            WriterStream = New StreamWriter(NutStream)
            ConnectionStatus = True
        Catch Excep As Exception
            RaiseEvent OnError(New Nut_Exception(Nut_Exception_Value.CONNECT_ERROR, Excep.Message), LogLvl.LOG_ERROR, Me)
            ConnectionStatus = False
        End Try
        Return ConnectionStatus
    End Function

    ' Parse and enumerate a NUT protocol response.
    Private Function EnumResponse(Data As String) As NUTResponse
        Dim Response As NUTResponse
        ' Remove hyphens to prepare for parsing.
        Dim SanitisedString = UCase(Data.Replace("-", String.Empty))
        ' Break the response down so we can get specifics.
        Dim SplitString = SanitisedString.Split(" "c)

        Select Case SplitString(0)
            Case "OK", "VAR", "BEGIN", "DESC", "UPS"
                Response = NUTResponse.OK
            Case "END"
                Response = NUTResponse.ENDLIST
            Case "ERR"
                Response = DirectCast([Enum].Parse(GetType(NUTResponse), SplitString(1)), NUTResponse)
            Case "NETWORK", "1.0", "1.1", "1.2"
                'In case of "VER" or "NETVER" Query
                Response = NUTResponse.OK
            Case Else
                ' We don't recognize the response, throw an error.
                Response = NUTResponse.NORESPONSE
                'Throw New Exception("Unknown response from NUT server: " & Response)
        End Select
        Return Response
    End Function

    Private Sub Event_WatchDog(sender As Object, e As EventArgs)
        Dim Nut_Query = Query_Data("")
        If Nut_Query.Response = NUTResponse.NORESPONSE Then
            ConnectionStatus = False
            Disconnect(True)
            RaiseEvent Socket_Broken()
        ElseIf Nut_Query.Response = NUTResponse.UNKNOWNCOMMAND Then
            ConnectionStatus = True
        End If
    End Sub

End Class
