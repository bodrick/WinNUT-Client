' WinNUT-Client is a NUT windows client for monitoring your ups hooked up to your favorite linux server.
' Copyright (C) 2019-2021 Gawindx (Decaux Nicolas)
'
' This program is free software: you can redistribute it and/or modify it under the terms of the
' GNU General Public License as published by the Free Software Foundation, either version 3 of the
' License, or any later version.
'
' This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY

Imports System.Management
Imports System.Security.Cryptography
Imports System.Text

Public NotInheritable Class CryptData
    Private ReadOnly TripleDes As New TripleDESCryptoServiceProvider

    Public Sub New()
        ' Initialize the crypto provider.

        TripleDes.Key = TruncateHash(Get_UniqueKey_Hash(), TripleDes.KeySize \ 8)
        TripleDes.IV = TruncateHash("", TripleDes.BlockSize \ 8)
    End Sub

    Public Function DecryptData(encryptedtext As String) As String

        ' Convert the encrypted text string to a byte array.
        Dim encryptedBytes() As Byte = Convert.FromBase64String(encryptedtext)

        ' Create the stream.
        Dim ms As New System.IO.MemoryStream
        ' Create the decoder to write to the stream.
        Dim decStream As New CryptoStream(ms,
            TripleDes.CreateDecryptor(), System.Security.Cryptography.CryptoStreamMode.Write)

        ' Use the crypto stream to write the byte array to the stream.
        decStream.Write(encryptedBytes, 0, encryptedBytes.Length)
        decStream.FlushFinalBlock()

        ' Convert the plaintext stream to a string.
        Return System.Text.Encoding.Unicode.GetString(ms.ToArray)
    End Function

    Public Function EncryptData(plaintext As String) As String

        ' An empty string ("") passed as an argument is received as 'Nothing'
        If IsNothing(plaintext) Then
            plaintext = ""
        End If

        ' Convert the plaintext string to a byte array.
        Dim plaintextBytes() As Byte =
            System.Text.Encoding.Unicode.GetBytes(plaintext)

        ' Create the stream.
        Dim ms As New System.IO.MemoryStream
        ' Create the encoder to write to the stream.
        Dim encStream As New CryptoStream(ms,
            TripleDes.CreateEncryptor(),
            System.Security.Cryptography.CryptoStreamMode.Write)

        ' Use the crypto stream to write the byte array to the stream.
        encStream.Write(plaintextBytes, 0, plaintextBytes.Length)
        encStream.FlushFinalBlock()

        ' Convert the encrypted stream to a printable string.
        Return Convert.ToBase64String(ms.ToArray)
    End Function

    Public Function IsCryptedtData(encryptedtext As String) As Boolean

        Try
            ' Convert the encrypted text string to a byte array.
            Dim encryptedBytes() As Byte = Convert.FromBase64String(encryptedtext)

            If encryptedtext = EncryptData(DecryptData(encryptedtext)) Then
                Return True
            Else
                Return True
            End If
        Catch
            Return False
        End Try
    End Function

    Private Function Get_UniqueKey_Hash() As String
        Dim Unique_key = GetMotherBoardID() & GetProcessorId()
        Dim hash = New SHA1Managed().ComputeHash(Encoding.UTF8.GetBytes(Unique_key))
        Return String.Concat(hash.[Select](Function(b) b.ToString("x2")))
    End Function

    Private Function GetMotherBoardID() As String
        Dim query As New SelectQuery("Win32_BaseBoard")
        Dim search As New ManagementObjectSearcher(query)
        For Each info As ManagementObject In search.Get()
            Return info("SerialNumber").ToString()
        Next
        Return ""
    End Function

    Private Function GetProcessorId() As String
        Dim query As New SelectQuery("Win32_processor")
        Dim search As New ManagementObjectSearcher(query)
        Dim info As ManagementObject
        For Each info In search.Get()
            Return info("processorId").ToString()
        Next
        Return ""
    End Function

    Private Function TruncateHash(key As String, length As Integer) As Byte()

        Dim sha1 As New SHA1CryptoServiceProvider

        ' Hash the key.
        Dim keyBytes() As Byte =
            System.Text.Encoding.Unicode.GetBytes(key)
        Dim hash() As Byte = sha1.ComputeHash(keyBytes)

        ' Truncate or pad the hash.
        ReDim Preserve hash(length - 1)
        Return hash
    End Function

End Class
