# Third-party notices

Medical Calendar includes the following third-party software. Each component's licence applies to that component only.

---

## PDFsharp and MigraDoc 6.2.4

Used to create PDF reports. NuGet packages `PDFsharp-MigraDoc` / `PDFsharp`, version 6.2.4.
<https://www.pdfsharp.com> · <https://github.com/empira/PDFsharp>

```
MIT License

Copyright (c) 2001-2026 empira Software GmbH, Troisdorf (Cologne Area), Germany

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Note: PDFsharp 6.5.0 and later are licensed under AGPL-3.0. This project deliberately stays on 6.2.4.

---

## .NET runtime and WPF

The release exe is self-contained and includes the .NET 10 runtime and Windows Presentation Foundation.
<https://github.com/dotnet/runtime> · <https://github.com/dotnet/wpf>

```
The MIT License (MIT)

Copyright (c) .NET Foundation and Contributors

All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

The .NET runtime also bundles components under their own licences; see the `THIRD-PARTY-NOTICES.TXT` file in the dotnet/runtime repository.

---

## Fonts

PDF reports embed subsets of fonts already installed with Windows (Segoe UI; for Japanese text Yu Gothic, Meiryo or MS Gothic). No font files are shipped with the app.
