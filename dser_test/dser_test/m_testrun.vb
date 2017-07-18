
Module testrun

    Public Const NullDate As Date = #7/7/0007#

    Sub Main()
        Dim d As New Dataseries.DateTimeSeries(Of Integer)(New Date(2017, 7, 14, 0, 0, 0), New Date(2017, 7, 14, 0, 30, 0), 0)

        Dim gufi As gufo() = {New gufo(1, New Date(2017, 7, 14, 0, 4, 0), NullDate),
                              New gufo(2, New Date(2017, 7, 14, 0, 10, 0), New Date(2017, 7, 14, 0, 20, 0))}

        d.ReadIntervals(gufi.Select(Function(gufo) gufo.value).ToArray,
                        gufi.Select(Function(gufo) gufo.fdat).ToArray,
                        gufi.Select(Function(gufo) gufo.ldat).ToArray,
                        gufi.Select(Function(gufo) DirectCast(gufo.cdat, IComparable)).ToArray)

        Dim test As Integer
        test = d.GetValue(#7/14/2017 12:00:00 AM#)
        test = d.GetValue(#7/14/2017 12:04:00 AM#)
        test = d.GetValue(#7/14/2017 12:05:00 AM#)
        test = d.GetValue(#7/14/2017 12:17:00 AM#)
        test = d.GetValue(#7/14/2017 12:18:00 AM#)
        test = d.GetValue(#7/14/2017 12:30:00 AM#)

    End Sub

    Public Class gufo
        Public value As Integer
        Public fdat As Date
        Public ldat As Date
        Public cdat As Date
        'Public cdat As Integer

        Public Sub New(value As Integer, fdat As Date, ldat As Date)
            Me.value = value
            Me.fdat = fdat
            Me.ldat = ldat
            Me.cdat = Date.Now
            'Me.cdat = 1
        End Sub
    End Class

End Module
