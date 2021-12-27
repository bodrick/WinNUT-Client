' WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
' Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
'
' This program is free software: you can redistribute it and/or modify it under the terms of the
' GNU General Public License as published by the Free Software Foundation, either version 3 of the
' License, or any later version.
'
' This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

Public Class Logger
    Private ReadOnly LogFile As New Microsoft.VisualBasic.Logging.FileLogTraceListener()
    Private ReadOnly TEventCache As New TraceEventCache()

    ' Enable writing to a log file.
    Public WriteLogValue As Boolean

    Public LogLevelValue As LogLvl
    Private L_CurrentLogData As String
    Private ReadOnly LastEventsList As New List(Of Object)

    Public Event NewData(sender As Object)

    Public Property CurrentLogData() As String
        Get
            Dim Tmp_Data = L_CurrentLogData
            L_CurrentLogData = Nothing
            Return Tmp_Data
        End Get
        Set(Value As String)
            L_CurrentLogData = Value
        End Set
    End Property

    Public ReadOnly Property LastEvents() As List(Of Object)
        Get
            Return LastEventsList
        End Get
    End Property

    Public Sub New(WriteLog As Boolean, LogLevel As LogLvl)
        WriteLogValue = WriteLog
        LogLevelValue = LogLevel
        LogFile.TraceOutputOptions = TraceOptions.DateTime Or TraceOptions.ProcessId
        LogFile.Append = True
        LogFile.AutoFlush = True
        LogFile.BaseFileName = "WinNUT-CLient"
        LogFile.LogFileCreationSchedule = Logging.LogFileCreationScheduleOption.Daily
        LogFile.Location = Microsoft.VisualBasic.Logging.LogFileLocation.Custom
        LogFile.CustomLocation = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\WinNUT-Client"
        LastEventsList.Capacity = 50
        WinNUT_Globals.LogFilePath = LogFile.FullLogFileName
    End Sub

    Public Property WriteLog() As Boolean
        Get
            Return WriteLogValue
        End Get
        Set(Value As Boolean)
            WriteLogValue = Value
            If Not WriteLogValue Then
                LogFile.Dispose()
            End If
        End Set
    End Property

    Public Property LogLevel() As LogLvl
        Get
            Return LogLevelValue
        End Get
        Set(Value As LogLvl)
            LogLevelValue = Value
        End Set
    End Property

    Public Sub LogTracing(message As String, LvlError As Int16, sender As Object, Optional LogToDisplay As String = Nothing)
        Dim Pid = TEventCache.ProcessId
        Dim SenderName = sender.GetType.Name
        Dim EventTime = Now.ToLocalTime
        Dim FinalMsg = EventTime & " Pid: " & Pid & " " & SenderName & " : " & message

        'Update LogFilePath to make sure it's still the correct path
        WinNUT_Globals.LogFilePath = LogFile.FullLogFileName

        ' Always write log messages to the attached debug messages window.
#If DEBUG Then
        Debug.WriteLine(FinalMsg)
#End If

        'Create Event in EventList in case of crash for generate Report
        If LastEventsList.Count = LastEventsList.Capacity Then
            LastEventsList.RemoveAt(0)
        End If

        LastEventsList.Add(FinalMsg)

        If WriteLogValue AndAlso LogLevel >= LvlError Then
            LogFile.WriteLine(FinalMsg)
        End If
        'If LvlError = LogLvl.LOG_NOTICE Then
        If LogToDisplay IsNot Nothing Then
            L_CurrentLogData = LogToDisplay
            RaiseEvent NewData(sender)
        End If
    End Sub

End Class
