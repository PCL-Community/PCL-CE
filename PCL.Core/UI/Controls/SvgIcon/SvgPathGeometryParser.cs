using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PCL.Core.UI.Controls.SvgIcon;

internal static class SvgPathGeometryParser
{
    public static Geometry Parse(string data)
    {
        var parser = new Parser(data);
        return parser.Parse();
    }

    private static List<Token> _Tokenize(string data)
    {
        var tokens = new List<Token>();
        var index = 0;

        while (index < data.Length)
        {
            var ch = data[index];
            if (char.IsWhiteSpace(ch) || ch == ',')
            {
                index++;
                continue;
            }

            if (_IsCommand(ch))
            {
                tokens.Add(new Token(TokenKind.Command, ch, 0D));
                index++;
                continue;
            }

            var start = index;
            if (ch is '+' or '-')
                index++;

            var hasDigit = false;
            while (index < data.Length && char.IsDigit(data[index]))
            {
                index++;
                hasDigit = true;
            }

            if (index < data.Length && data[index] == '.')
            {
                index++;
                while (index < data.Length && char.IsDigit(data[index]))
                {
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                throw new FormatException($"Invalid SVG path number near index {start}.");

            if (index < data.Length && data[index] is 'e' or 'E')
            {
                var exponentStart = index;
                index++;
                if (index < data.Length && data[index] is '+' or '-')
                    index++;

                var exponentHasDigit = false;
                while (index < data.Length && char.IsDigit(data[index]))
                {
                    index++;
                    exponentHasDigit = true;
                }

                if (!exponentHasDigit)
                    index = exponentStart;
            }

            var numberText = data[start..index];
            tokens.Add(new Token(
                TokenKind.Number,
                '\0',
                double.Parse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture)));
        }

        return tokens;
    }

    private static bool _IsCommand(char ch)
    {
        return ch is 'M' or 'm'
            or 'Z' or 'z'
            or 'L' or 'l'
            or 'H' or 'h'
            or 'V' or 'v'
            or 'C' or 'c'
            or 'S' or 's'
            or 'Q' or 'q'
            or 'T' or 't'
            or 'A' or 'a';
    }

    private enum TokenKind
    {
        Command,
        Number
    }

    private readonly record struct Token(TokenKind Kind, char Command, double Number);

    private sealed class Parser(string data)
    {
        private readonly PathGeometry _geometry = new();
        private readonly List<Token> _tokens = _Tokenize(data);
        private Point _current;
        private PathFigure? _figure;
        private bool _figureOpen;
        private Point _figureStart;
        private int _index;
        private char _lastCommand;
        private Point? _lastCubicControl;
        private Point? _lastQuadraticControl;

        public Geometry Parse()
        {
            var command = '\0';
            while (_index < _tokens.Count)
            {
                if (_PeekCommand(out var nextCommand))
                {
                    command = nextCommand;
                    _index++;
                }
                else if (command == '\0')
                {
                    throw new FormatException("SVG path data must start with a command.");
                }

                _ExecuteCommand(command);
            }

            return _geometry;
        }

        private void _ExecuteCommand(char command)
        {
            switch (command)
            {
                case 'M':
                case 'm':
                    _MoveTo(command == 'm');
                    break;
                case 'L':
                case 'l':
                    _LineTo(command == 'l');
                    break;
                case 'H':
                case 'h':
                    _HorizontalLineTo(command == 'h');
                    break;
                case 'V':
                case 'v':
                    _VerticalLineTo(command == 'v');
                    break;
                case 'C':
                case 'c':
                    _CubicBezierTo(command == 'c');
                    break;
                case 'S':
                case 's':
                    _SmoothCubicBezierTo(command == 's');
                    break;
                case 'Q':
                case 'q':
                    _QuadraticBezierTo(command == 'q');
                    break;
                case 'T':
                case 't':
                    _SmoothQuadraticBezierTo(command == 't');
                    break;
                case 'A':
                case 'a':
                    _ArcTo(command == 'a');
                    break;
                case 'Z':
                case 'z':
                    _CloseFigure();
                    break;
                default:
                    throw new FormatException($"Unsupported SVG path command: {command}");
            }
        }

        private void _MoveTo(bool relative)
        {
            if (!_HasNumber())
                return;

            var first = _ReadPoint(relative);
            _BeginFigure(first);
            _SetLastCommand('M');

            // SVG 规范：M/m 后续坐标对等同 L/l。
            while (_HasNumber())
                _AddLine(_ReadPoint(relative));
        }

        private void _LineTo(bool relative)
        {
            while (_HasNumber())
                _AddLine(_ReadPoint(relative));
            _SetLastCommand('L');
        }

        private void _HorizontalLineTo(bool relative)
        {
            while (_HasNumber())
            {
                var x = _ReadNumber();
                _AddLine(new Point(relative ? _current.X + x : x, _current.Y));
            }

            _SetLastCommand('H');
        }

        private void _VerticalLineTo(bool relative)
        {
            while (_HasNumber())
            {
                var y = _ReadNumber();
                _AddLine(_current with { Y = relative ? _current.Y + y : y });
            }

            _SetLastCommand('V');
        }

        private void _CubicBezierTo(bool relative)
        {
            while (_HasNumber())
            {
                var control1 = _ReadPoint(relative);
                var control2 = _ReadPoint(relative);
                var end = _ReadPoint(relative);
                _EnsureFigure();

                _figure!.Segments.Add(new BezierSegment(control1, control2, end, true));
                _current = end;
                _lastCubicControl = control2;
                _lastQuadraticControl = null;
                _lastCommand = 'C';
            }
        }

        private void _SmoothCubicBezierTo(bool relative)
        {
            while (_HasNumber())
            {
                var control1 = _lastCommand is 'C' or 'S' && _lastCubicControl is not null
                    ? _Reflect(_lastCubicControl.Value, _current)
                    : _current;
                var control2 = _ReadPoint(relative);
                var end = _ReadPoint(relative);

                _EnsureFigure();
                _figure!.Segments.Add(new BezierSegment(control1, control2, end, true));
                _current = end;
                _lastCubicControl = control2;
                _lastQuadraticControl = null;
                _lastCommand = 'S';
            }
        }

        private void _QuadraticBezierTo(bool relative)
        {
            while (_HasNumber())
            {
                var control = _ReadPoint(relative);
                var end = _ReadPoint(relative);
                _EnsureFigure();

                _figure!.Segments.Add(new QuadraticBezierSegment(control, end, true));
                _current = end;
                _lastQuadraticControl = control;
                _lastCubicControl = null;
                _lastCommand = 'Q';
            }
        }

        private void _SmoothQuadraticBezierTo(bool relative)
        {
            while (_HasNumber())
            {
                var control = _lastCommand is 'Q' or 'T' && _lastQuadraticControl is not null
                    ? _Reflect(_lastQuadraticControl.Value, _current)
                    : _current;
                var end = _ReadPoint(relative);
                _EnsureFigure();

                _figure!.Segments.Add(new QuadraticBezierSegment(control, end, true));
                _current = end;
                _lastQuadraticControl = control;
                _lastCubicControl = null;
                _lastCommand = 'T';
            }
        }

        private void _ArcTo(bool relative)
        {
            while (_HasNumber())
            {
                var rx = Math.Abs(_ReadNumber());
                var ry = Math.Abs(_ReadNumber());
                var rotation = _ReadNumber();
                var isLargeArc = Math.Abs(_ReadNumber()) > 0D;
                var isClockwise = Math.Abs(_ReadNumber()) > 0D;
                var end = _ReadPoint(relative);

                _EnsureFigure();
                if (rx <= 0D || ry <= 0D)
                    _figure!.Segments.Add(new LineSegment(end, true));
                else
                    _figure!.Segments.Add(new ArcSegment(
                        end,
                        new Size(rx, ry),
                        rotation,
                        isLargeArc,
                        isClockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                        true));

                _current = end;
                _lastCubicControl = null;
                _lastQuadraticControl = null;
                _lastCommand = 'A';
            }
        }

        private void _CloseFigure()
        {
            if (_figureOpen && _figure is not null)
            {
                _figure.IsClosed = true;
                _current = _figureStart;
                _figureOpen = false;
            }

            _SetLastCommand('Z');
        }

        private void _BeginFigure(Point point)
        {
            _figure = new PathFigure
            {
                StartPoint = point,
                IsClosed = false,
                IsFilled = true
            };
            _geometry.Figures.Add(_figure);
            _current = point;
            _figureStart = point;
            _figureOpen = true;
            _lastCubicControl = null;
            _lastQuadraticControl = null;
        }

        private void _EnsureFigure()
        {
            if (_figureOpen && _figure is not null)
                return;

            _BeginFigure(_current);
        }

        private void _AddLine(Point point)
        {
            _EnsureFigure();
            _figure!.Segments.Add(new LineSegment(point, true));
            _current = point;
            _lastCubicControl = null;
            _lastQuadraticControl = null;
            _lastCommand = 'L';
        }

        private Point _ReadPoint(bool relative)
        {
            var x = _ReadNumber();
            var y = _ReadNumber();
            return relative ? new Point(_current.X + x, _current.Y + y) : new Point(x, y);
        }

        private double _ReadNumber()
        {
            return _HasNumber()
                ? _tokens[_index++].Number
                : throw new FormatException("Expected number in SVG path data.");
        }

        private bool _HasNumber()
        {
            return _index < _tokens.Count && _tokens[_index].Kind == TokenKind.Number;
        }

        private bool _PeekCommand(out char command)
        {
            if (_index < _tokens.Count && _tokens[_index].Kind == TokenKind.Command)
            {
                command = _tokens[_index].Command;
                return true;
            }

            command = '\0';
            return false;
        }

        private void _SetLastCommand(char command)
        {
            _lastCommand = command;
            if (command is not ('C' or 'S'))
                _lastCubicControl = null;
            if (command is not ('Q' or 'T'))
                _lastQuadraticControl = null;
        }

        private static Point _Reflect(Point point, Point around)
        {
            return new Point(around.X * 2D - point.X, around.Y * 2D - point.Y);
        }
    }
}