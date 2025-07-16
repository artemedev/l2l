

namespace MD.Aggregation.Devices.Printer.ZPL;

// Возможно можно использовать. Дабы не забыть...
// using System.IO.Ports;
// Parity.Even;
// StopBits.One;


public class CommunicationSettings
{
    /// <summary>
    /// Установка соединения по DTR.
    /// true - DTR; false - Xon/Xoff.
    /// Бит 7.
    /// </summary>
    public bool HandshakeDTR { get; set; } = true;

    /// <summary>
    /// Четность.
    /// true - Even(сетный); false - Odd(нечётный).
    /// Бит 6.
    /// </summary>
    public bool ParityEven { get; set; } = true;

    /// <summary>
    /// true - Enable; false - Disable.
    /// Бит 5.
    /// </summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// Количество стоп-битов.
    /// true - 1 бит, false - 2 бита.
    /// Бит 4
    /// </summary>
    public bool StopBits { get; set; } = true;

    /// <summary>
    /// Количество битов данных:
    /// true - 8 бит, false - 7 бит.
    /// Бит 3
    /// </summary>
    public bool DataBits { get; set; } = true;

    /// <summary>
    /// Скорость.
    /// Биты: 8 210
    ///       0 000 = 110
    ///       0 001 = 300
    ///       0 010 = 600
    ///       0 011 = 1200
    ///       0 100 = 2400
    ///       0 101 = 4800
    ///       0 110 = 9600
    ///       0 111 = 19200
    ///       1 000 = 28800
    ///       1 001 = 38400
    ///       1 010 = 57600
    ///       1 011 = 14400
    /// </summary>
    public int Baud { get; set; } = 0;
}
