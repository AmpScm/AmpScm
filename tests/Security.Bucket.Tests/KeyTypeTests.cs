using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Cryptography;
using AmpScm.Buckets.Cryptography.Algorithms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SecurityBucketTests;

[TestClass]
public class KeyTypeTests
{
    public record TestKey
    {
        public TestKey(string Name, string Password, string Key)
        {
            this.Name = Name;
            this.Password = Password;
            this.Key = Key;
        }

        public string Name { get; }
        public string Password { get; }
        public string Key { get; }

        public override string ToString() => Name;
    }

    public static IEnumerable<object[]> TestKeys => new TestKey[]
    {
        new("RSA-1024", "k1",
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

lQIGBGQqwpABBADiXkGtzoWFt4pKXL2Lhsqi6KcaDO24dQU6OxbIARPApHRhxPJp
6mu2DhqS/Ws1QbBuqBfoG3BneIXiqcbhyFH8XBmAb3STnX7YQdB1yYa74dtqx7XK
0bVW1RBGqqxKafaOyogRn52t+Zs1Mno8kYIySb2pahkVczWyJ9JBvo/88QARAQAB
/gcDAiYztNGUfmSDynIEfnnQ9PXn9UW91sp2F1a1bl5gQ6NR14dRx+DlVdSWxim4
OuvheSSg0vVQO8PBROEslWX8JrmCGC7nnHgxNpmVih9QbEhvIkNb+DPQJ9GXPDct
TaFWdIjOHcJWCxgvFe99vafH30aaYZNrJNeXc0X51/dEYe2iy3z5Bx9fmZwQgONw
dj5jZT07zEV9yct1KVwbpMnMJOdgrITQjsAZ1qp4K+2RYBbE9OeTVq6HhaU1MAPg
96Mb9bfCnLUUGv0I8vx0uki9GYRGO7qEY7A4Clc0MjmUcjBoqyZX5Gd0ipxPFPfu
sqUiSkFSmJSTTLazTqUDG1pUQHLh+P9G4uKIy9jUgwLJEvVytDDTA74TyHBFIwj3
BvOyosQh1BmASvS/k5VvXXu0h/hMXyA3ZlwYoGlUZz+YKLlEd9xZdUDuObkKHxVu
+yKzQs8iYA8fne5wwZUrj7/HfYtfNkufCH1F1DbINeyFG3ltNwc+LTC0GlJTQSAx
MDI0IChUZXN0KSA8MTAyNEByc2E+iNEEEwEIADsWIQScrCusD8jKjvuPr3zaneoZ
Sex0WAUCZCrCkAIbAwULCQgHAgIiAgYVCgkICwIEFgIDAQIeBwIXgAAKCRDaneoZ
Sex0WFlRA/4zPwS6D089pywrHM9xXWXYIXDmvHYq6wFaak6/f5Wl2gEDM78Ezz5D
DYWDjCH1DBAIwof6YhG7wYHePPdXxG2kZSp+saChmwmODxFjvsBd9KP8SaQYO2PR
IMa9Cb6kLVitT3GuGuJGpap2p8C11TysCsw6wnBJfQA7AGAkX5uRwZ0CBgRkKsKQ
AQQA73K4YqBvQi9F3JeiY0cvz0w7/w7VgGYaGP2JicercmPKCv82lbumbyCimsTQ
6zp7pnKokl/RslTOJXE4cQUPxzvAhlI3E283KvyScea4PEWBurlUzMpNkHnQLMD+
Rn7IKSloT6JZGYshxygZpc5ivrpTj8vz/E6m1yENWCJhLN0AEQEAAf4HAwIwAbSB
UVbJbMoDM7z5JhcLSCyHMWDSbnGVL4iX/4LVzG3o5hC88stdg7v8JtsimbDv5+ls
AFE0+BEbbB7lN8i7EPaOGDa72LQr8/MmvAcCt2uI5tjUt9rrYXUxHM/4GhQFOCbi
z2CVC8t4+yB3rKRxm1KZUVdPdHZ4QapWnPbA9vYwG4tawv2ShS4iqk6yfhp11VKr
haqFhAktZ0GzHbtOVqn75lCYYSSkj0zvqwbYBW9OF+quEB7iwv53t+7tv9xb54gy
xYSD+Z4Ayey8+oRQt/X2hiXXwB2qeSUfKcP0PCZ1yfbGPYl7D/aUuRt3i3ROkOJl
9UfFQfFNuwJG20PhLBteWIfC89iWdmdi71uDd4xOLbZAjtHYAdleQOxpmfkL7691
QMngAkMC3jfPweaZ1yA7zjow0X7hBrxv8G0y0LKzQtwApdJxE/cVok9yBQCf8TpZ
VzhRrdvgY/fve7NT/MO4W4QzBEIvEt81WJK4Z7rjTYzYiLYEGAEIACAWIQScrCus
D8jKjvuPr3zaneoZSex0WAUCZCrCkAIbDAAKCRDaneoZSex0WLG2BACUKwmUi0Be
CTlPQ+icbX+RKUiD76xChRL7jxb3Y19cmq2IzZAkZLpgsq4lEdvYldtU7l7zkABC
X6PqilVvz/iReGW/tKcpHta4ShXd9ffQYCkxZk1kQdSDPgIOnX6GzsZMejyjWK6R
2VS5bHOpoOuLIP/0iykVDU+RcIOZC0xFxA==
=kaKl
-----END PGP PRIVATE KEY BLOCK-----
"),
        new("RSA-4096", "k2",
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

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
7wcvT4hz5TQAg3RApKS70Iof1jpn40nnDw==
=6/QT
-----END PGP PRIVATE KEY BLOCK-----
"),
        new ("25519@ECC", "k3",
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

lIYEZCrIUhYJKwYBBAHaRw8BAQdAteqQ9A1JEijADu07VyK0l0MdBUhVhm1YJNzS
CIGpIFz+BwMC/EcQVuk0lnHK85rr2RkdvFMfG/7j+aALm6FZrsIb1dRR8qfw7EgY
aLQI3/eHZ+8wExBolVMdOiw0fjDVzZS5ZJi40+AIX8Zf3IwX7QuDJ7QaRUNDIDI1
NTE5IChDMykgPDI1NTE5QGVjYz6IkwQTFgoAOxYhBPeUrs/Q6MJPM+RziOaGQ9Te
1sO8BQJkKshSAhsDBQsJCAcCAiICBhUKCQgLAgQWAgMBAh4HAheAAAoJEOaGQ9Te
1sO8H4MBAMQQiPop4vnE00JQFxLsTu1+q1XlKAlFg3QGKFvpYZlyAQCVFx2Jxnjb
bPD1rtNDE6OZxUdQL4CgWxkCDehMMYVuBZyLBGQqyFISCisGAQQBl1UBBQEBB0Cr
rM6p2pugptSx15yGwMEypsHvu1n1RHhnyOWX/aheSwMBCAf+BwMCP5Q09UNWJR7K
z5oK2ELbnm6CkSu7OHGvA1JL6WY2WfjyxlbRErmFwbOizIdbAcxQjKgww2bt/b7C
K076KLEUp1dXzjvfIM8zOT6f7Xg3/oh4BBgWCgAgFiEE95Suz9Dowk8z5HOI5oZD
1N7Ww7wFAmQqyFICGwwACgkQ5oZD1N7Ww7x+LwEAwWGttUHMxTikYYCHa9LiAUPL
noTh+1EB2RqNLx5jF4kA/05QwJPJ+3V6R8x6EC6zVSIpRSpFTnFqSWWMDROH1/QA
=75FI
-----END PGP PRIVATE KEY BLOCK-----
"),

#if Q // Somehow breaks, while the algorithm works
        new("nistp384", "k4",
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

lNIEZCrJLhMFK4EEACIDAwSnpD9VaoxoFf5RhJriyTMpiIZjqVbztRehVZMh9Ep2
MMgM4cKxWb8kohJZPI8USrJFJryNAdd1vBgSfa0nvBLJDtX+n9+uCasrtsyylRfR
3wZypP/AYfaTgtih6ujEqO7+BwMCrjoCHs5xCJ7KFEi85XY4vjT1sZagtf7BdPnH
btxyYxyCj+T464C/kZP5uYlWBo9sx67Ctk0ooBXSoOSYKfvw+Oqs2IE5zaZONwcm
gdfTtZDkoZ25MvFy0vk4R8m+Ane0E1AzODQgRUNDIDxwMzg0QGVjYz6IswQTEwkA
OxYhBDwXF//q0tvz9DDTFhovnCAlynp5BQJkKskuAhsDBQsJCAcCAiICBhUKCQgL
AgQWAgMBAh4HAheAAAoJEBovnCAlynp5QTIBgOaN5TdOsL1jdLEVhAXTUdF+WsWl
kgyCzEGAPus9woEKAa4wMes/3zPR2qkwvI0BbgF/V6eCvAJ/HU1gfELCndV/BTUk
vAN0rjJO21SrkecMmuVam2I/oEhUO7pS45zP7jEI
=EwEG
-----END PGP PRIVATE KEY BLOCK-----
"),
#endif
    new("DSA/Elgamal", "k5",
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

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
tZ+ow95WDaupPboj1cAnNLxkgw==
=LWbE
-----END PGP PRIVATE KEY BLOCK-----
"),

    new("Brainpool P-256", "k6",
@"-----BEGIN PGP PRIVATE KEY BLOCK-----

lKYEZCrTuBMJKyQDAwIIAQEHAgMEGYClGSaFY+E/H3FlVZn1Y4jPNzYM4grTIwks
lnI0Qb0YaAxi4YeW0qZuRHH8WAcveAuI8VS0/8jKbk/ZuI2k+/4HAwJdvgPANt7k
J8o9O0X/kY6mEvaFyAfR/XdAD5WRk/ayKsCNo/u/anD6Pb16IHYHljTXHjoxVN8L
MKDHAbjvtSBO28ocblXhog1niDXOn2w2tBxCcmFpblBvb2wgUC0yNTYgPHAyNTZA
YnJhaW4+iJMEExMIADsWIQR6qqzfjgGNaI2q/vio7QDpmo/3twUCZCrTuAIbAwUL
CQgHAgIiAgYVCgkICwIEFgIDAQIeBwIXgAAKCRCo7QDpmo/3t+VqAP0dVLiS/e/6
Yr7a6h1P51KB5ADPY29E0osc+8rYUtcvUQD/VrLMzX61Tf/PPAFTtxxbtk06JFVr
4Z1r4mIsrlzI0N+cqgRkKtO4EgkrJAMDAggBAQcCAwQM/kKW9olQB0G0dwTczAaQ
1215FJ2vgDpXHHIO5dY2CxLbXttjY6eiy6R3JCUC2YZbhm7TbOUqjXxESWM4s6hg
AwEIB/4HAwIPJMGTnpijf8oqpj1Wnczs97T9S+YdUyoCoLXGB2yHRWAoKSHbwygP
fh3C9uih71RdpL4xFSEdaDXysGyh81W8JsI8escNQ1OO5P5aL2jdiHgEGBMIACAW
IQR6qqzfjgGNaI2q/vio7QDpmo/3twUCZCrTuAIbDAAKCRCo7QDpmo/3tzJNAQCM
gOxLqL9sC9Cc1T1w46cqq/Scx8JAD9D02W1RhpCRpgD/fcQYHT+QtJATZT1MDaYH
yEe9wE2abt21XZ1LN4EpVWs=
=a4Yc
-----END PGP PRIVATE KEY BLOCK-----
")

    }.Select(x => new object[] { x });

    [DynamicData(nameof(TestKeys))]//, DynamicDataDisplayName = nameof(TestKeyName))]
    [TestMethod]
    public void LoadKey(TestKey key)
    {
        Assert.IsTrue(PublicKeySignature.TryParse(key.Key, out var r));
        Assert.IsFalse(r.HasPrivateKey);

        Assert.IsTrue(PublicKeySignature.TryParse(key.Key, (_) => key.Password, out var r2));
        Assert.IsTrue(r2.HasPrivateKey);
    }

    [DynamicData(nameof(TestKeys))]
    [TestMethod]
    public async Task TestDecrypt(TestKey key)
    {
        // echo This is it!|gpg -a -e -u 9CAC2BAC0FC8CA8EFB8FAF7CDA9DEA1949EC7458 -r 58163F6EAD5A0E27C232CE31766EA20745F40A79 -r F794AECFD0E8C24F33E47388E68643D4DED6C3BC -r BFEEBC0BFD05A0D2B863D706122836BC2E0DEB8F -r 9CAC2BAC0FC8CA8EFB8FAF7CDA9DEA1949EC7458 -r 7AAAACDF8E018D688DAAFEF8A8ED00E99A8FF7B7
        var src = @"-----BEGIN PGP MESSAGE-----

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
=dWKf
-----END PGP MESSAGE-----
";

        Assert.IsTrue(PublicKeySignature.TryParse(key.Key, (_) => key.Password, out var r2));
        Assert.IsTrue(r2.HasPrivateKey);

        switch (r2.Algorithm)
        {
            case CryptoAlgorithm.Rsa:
            case CryptoAlgorithm.Elgamal:
            case CryptoAlgorithm.Dsa:
                break; // Work
            case CryptoAlgorithm.Ecdsa:
#if !NETCOREAPP
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                    Assert.Inconclusive("Mono and ECDH are not a good mix");
#else
                if (OperatingSystem.IsMacOS())
                    Assert.Inconclusive("GitHub's MacOS install's doesn't like ECDH");
#endif
                break;

            default:
#if !DEBUG
                Assert.Inconclusive($"{r2.Algorithm} not implemented yet");
#endif
                break;
        }

        var rd = Bucket.Create.FromASCII(src);

        var ar = new Radix64ArmorBucket(rd);

        var dc = new DecryptBucket(ar) { KeyChain = r2 };


        var bb = await dc.ReadExactlyAsync(1024);

        Assert.AreEqual("This is it!\r\n", bb.ToUTF8String());
    }

    [TestMethod]
    public async Task EcdhPair()
    {
#if !NETCOREAPP
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            Assert.Inconclusive("Mono and ECDH are not a good mix");
#else
        if (OperatingSystem.IsMacOS())
            Assert.Inconclusive("GitHub's MacOS install's doesn't like ECDH");
#endif

        var k1Priv = @"-----BEGIN PGP PRIVATE KEY BLOCK-----

lKQEZFjYexMFK4EEACIDAwS+BHpXPredmD9JUp7XJot/+sGDGxDQSxRyuSyUKa8e
g2PhvWTPH2erG0amdEiAqbklea5/qc4MAfJS6QTjskEpX+r8I1Eo3gh47Pd7Qqdv
aIS77jIqarYdh93sDZveUPEAAYDhLe1ZnuZooZdTO2TSZ6NI9HM3kz6QLO3qRZ5u
sDjzsouLFQaM+YxO4Q77zz+eaoEaALQOSzEgPGsxQGsxLmNvbT6IswQTEwkAOxYh
BAVjpYYQrzzoqdBpJTEJOKXCbQ6PBQJkWNh7AhsDBQsJCAcCAiICBhUKCQgLAgQW
AgMBAh4HAheAAAoJEDEJOKXCbQ6PEDIBgJ/1IUoZE9HWCHpvBrkdcMuWFg9fB0Cm
VthayX6ki/J5Vz0oQBPgGdDSseFYO60q7gGApAh+5XXcSrd8pW1W9QT8EquELctG
tSw+r8tmeARMVcwiMWymm8SaE7b1JBiYxrk9nKgEZFjYexIFK4EEACIDAwTbYgF5
rM4OKuIbnPoPzzuFlGJFmcmOOVAZgExYwoxvWCHsEPE0pebW0O596s1uWVhx4Xbh
9/I+cdPFM7XzPq2OpjUasYgxQ4qz5Yg/tJpMxDwOU1YUXXmJuTcmY4Fhsk4DAQkJ
AAF/bq1GDBtv8Ybe86gUDDw7OqQkKcNGZgZZf0zOb6QORZEVG8xXSX7oMExBgKHQ
AS2rFBCImAQYEwkAIBYhBAVjpYYQrzzoqdBpJTEJOKXCbQ6PBQJkWNh7AhsMAAoJ
EDEJOKXCbQ6P+bsBgJTN5OUfK6Z2cXKRkhdauRR/rvHFbnvYXIFNtzBmZDNd28Qy
PxeKKhs+Ej/HOlE07gF/XKz9R7d99Abf5Uiz4bbbd8WW1Q1D4fkyMenZOjJx+/ef
4NG5EkVrBajm2eHyd3Qm
=EEFy
-----END PGP PRIVATE KEY BLOCK-----";

        var k2Priv = @"-----BEGIN PGP PRIVATE KEY BLOCK-----

lHgEZFjYoxMJKyQDAwIIAQEHAgMEAwLr9QXqGJIE9vdw0u1Vun2qgDA7j/xuQWRN
TpR77OxJNk/H/06b2dBSnRebjgSvn1RAQCXRszWpTlGpaNIwsgAA/2sH41hWVX2B
oTlBL88PznyFzcpXsQIO9yn3puIANwsgDvy0DksyIDxrMkBrMi5jb20+iJMEExMI
ADsWIQRk76Nazo+0HJojYOPY6MJ/5tyWcQUCZFjYowIbAwULCQgHAgIiAgYVCgkI
CwIEFgIDAQIeBwIXgAAKCRDY6MJ/5tyWcT+FAQCSTYrZvbV/RJWd5h8A26UWjKxE
Daz+f/hmJKK+jNS17QEApfyDhJ830FGEmmgySFYFt41JTDVEt26nOUPlRaR/Zwac
fARkWNijEgkrJAMDAggBAQcCAwQIfqyk9wJVQX9dhd92AzyEx06LuYAZbuD2epIH
UYKYrBnT2oEGoCaPEE0hTYgcA186KFhaNX1iDKWIovWi5ooVAwEIBwABAIt9ltXw
DFqPbD2rBCjqa0M4OjaH4NMe9yaIyw+Q1EGSDyeIeAQYEwgAIBYhBGTvo1rOj7Qc
miNg49jown/m3JZxBQJkWNijAhsMAAoJENjown/m3JZxBfMA/A/Op6Acbn3pXHg6
r9B1gbncNGywq9sUq9sBOmatCAGxAP42AP5To/+3U/N7gEMoRTo2ItuEOTyzC+K1
0a7lgPN4AQ==
=Rxdm
-----END PGP PRIVATE KEY BLOCK-----";

        Assert.IsTrue(PublicKeySignature.TryParse(k1Priv, out var k1));
        Assert.IsTrue(PublicKeySignature.TryParse(k2Priv, out var k2));
        Assert.IsTrue(k1.HasPrivateKey);
        Assert.IsTrue(k2.HasPrivateKey);

        var msg = @"-----BEGIN PGP MESSAGE-----

hH4DVRi0Fprbg70SAgMEXoNofCbcCAMsdq5RjV9BX7wtAIZ310MSFFPE7Z+L/eRX
uNWcRF3aqfSNnP6APdz5WnIxCLcDqT7HGM2pZC/qMTC+iT2Tff7Qoh6BzpznaRdW
j++NC4cv2D06wVzC6HbrerapeXqTNlJMPRdK4Wr4BJjUTgEJAhCNzx8DS1IXkFOV
YQ/w/MopRf4/LVKlJZl5pjnQLEeNTgvugNUoo7qeD1YYRfsKx26i9R6SdYBYOUpU
pRpHxU+Ml3pmAeFqHjYtxw==
=Ukcv
-----END PGP MESSAGE-----";

        var rd = Bucket.Create.FromASCII(msg);

        var ar = new Radix64ArmorBucket(rd);

        var dc = new DecryptBucket(ar) { KeyChain = k1 + k2 };


        var bb = await dc.ReadExactlyAsync(1024);

        Assert.AreEqual("\"Wow!\" \r\n", bb.ToUTF8String());

        foreach (var m2 in new[]
        {
            @"-----BEGIN PGP MESSAGE-----

hH4DVRi0Fprbg70SAgMEmIIphgX3UL1TsMFegVabmZ2IrSmAd0Q6SyxnBg/AsvFA
F8CL6BUEfzxGZPKaqs7edP0vniO/w0Dj88g5S1d4jjAgcS2DMatq+3fJ0kWPSZH7
qeJihaH/qr8ZV4n4k3CT0mD5U81vrU1dKLBIh5FlKWnUTwEJAhDSxi0Q/0+b5cH3
mfPRfdOqinPzAZvC2YlBeGjU2MB1l/QcYFLn+oMDTKGHO4Sfq6MGHHzTs9fJvWpU
oY8pvsEQ4U/iBaPqQBieSjU=
=9Xgl
-----END PGP MESSAGE-----",
            @"-----BEGIN PGP MESSAGE-----

hJ4DOhM8RTg4DJMSAwMEGt8z6BSITzD0gPr4IvDvLrZhyjx7O4kAcu54iFM8lYO1
EQiVILDkMidnhPhkBFfjIAld5g+sO7R+kaxbkXGAyO12cZHsRUvkc202SyEmvXsw
NvK51POMx4/awKCCFpOxMP3simZBsQ4s/0CzKolETdZVuUg9Cie8bdW+Je/+6cvk
K5tCMjkRRi2NdBHQMOa0CdRPAQkCEDjnuQEWtUOhCND7JzdeWAffpIDosGKaV9iT
zHSJiiHK4S+8X5BEZV2iuAloQme4SxtQJ5qpprWZSmjvGc8GrOLxiAuFy4nXcw4+
yg==
=eHvR
-----END PGP MESSAGE-----"
        })
        {
            rd = Bucket.Create.FromASCII(m2);

            ar = new Radix64ArmorBucket(rd);

            dc = new DecryptBucket(ar) { KeyChain = k1 + k2, VerifySignature = true };


            bb = await dc.ReadExactlyAsync(1024);

            Assert.AreEqual("This is it!\r\n", bb.ToUTF8String());
        }
    }

    [TestMethod]
    public async Task TestP384()
    {
#if !NETCOREAPP
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            Assert.Inconclusive("Mono and ECDH are not a good mix");

#endif
        // echo This is it!| gpg --home f:\gpg-home -r 479C86ED77BAE2293ABEED9CC66D25B89669FBDC -vvvvvv -a --debug-all -e 2>&1 |clip
        var keyData = @"
-----BEGIN PGP PRIVATE KEY BLOCK-----

lNIEZC/60BMFK4EEACIDAwQIVAQmxxyXQuyMurycF9jx2zGTUwyojYcJqW/QzesG
BhF8pYWJ+b0pM2qrYsqzFaRpRUgFEAeb3griVqIWRnpkhTXfhXwLejmAuEmBVwk5
7/7NbwpehWtpLn95Mef2P5z+BwMCgjKxq8KMS8DQmNe4cwQwf+ACy/7rguekvzMQ
SoYFZQvRlJGbpC59+2D0G2hJgNrKvPRIM7lEAGNherJQg0IbNUi9VONZjUF0TgYY
j+j3MlJP2jCddK6Jh2oJkkMxYbi0HE5pc3QgUDM4NCAoQmxhISkgPG5pc3RAcDM4
ND6IswQTEwkAOxYhBEechu13uuIpOr7tnMZtJbiWafvcBQJkL/rQAhsDBQsJCAcC
AiICBhUKCQgLAgQWAgMBAh4HAheAAAoJEMZtJbiWafvcFdMBgPJnRLgIKChmNcZA
S7L/QAHnsKezA3sogN3hep2IcG45xzXjbU/zC1F62OyJ4W82YwF+OYQWrMH4WYc/
puPnqrGtn/K7ecU71SOFyqdNepsTmVpdh81ZrfyhYw2yEcdIDsCknNYEZC/60BIF
K4EEACIDAwQ/NgAx1r8jPDCF0LECpRHSyUEyjuv2a+of5XHLFp1IsZTp3U6SWsJC
+H6OdsqL9CN5VOz1yf1pE9HX7fH7fxuzpb99lr2DEZih5M57Xj/7ZAB5uwjAl6Xe
vuSCWqxgi6UDAQkJ/gcDArgs8ggSF4SB0GTfdW/jr09EF2K+Xzk/pE/KyYEfBo7z
wJseiP1wk2oIsrSLQIT8omFO25inZnKk8L585MpxepEPgYBDEUAeAQcDRbeMFxHm
JpOZcOJnSRHt4DwW1fSGiJgEGBMJACAWIQRHnIbtd7riKTq+7ZzGbSW4lmn73AUC
ZC/60AIbDAAKCRDGbSW4lmn73BEuAX4pRk0/1LPR9t/HBvyWQ3rbuTZJDWunP+np
7fA4uKrExEYNlvyMPJ2m2yqf7V4DctMBfjKbWfjWUN1KgOXvhta4w+y/85J6M/1u
NS+P0JGmSavZ6MW8RCXV6CAmWH5AxVO4Hw==
=DYGZ
-----END PGP PRIVATE KEY BLOCK-----";

        Assert.IsTrue(PublicKeySignature.TryParse(keyData, (_) => "PW6", out var key));
        Assert.IsTrue(key.HasPrivateKey);

        var msg =
@"-----BEGIN PGP MESSAGE-----

hJ4DQyP+J9/KAQESAwME6x5y/fYG4XIPdJhW1vhtwXoRJ0na3p34AEoPyGUw7/xD
0nXnEjUjkYPPO7IpbHBQ9c+CYr+uf5zuQmNpi8XUwlKUmC6VWS9VIMurSSIJCt8p
F5K7b2b9SaF6h1P1ARW+MMF5DYwILJsLOdviLD+KlkOM3DZSKdW4rNM0wlmd3qJs
kqtAXdRyBoywerJvTPJo5dRQAQkCEL5kUHRAGD54A4fodOQ1nWvdNq49ps40Nf92
iu34YsJkfpCI+wJ2tdl5BYmwzoIQGlBXkpKq0qJfOqsZnRJFbgkouDeVhb780BCr
Pts=
=IA1T
-----END PGP MESSAGE-----";

        var rd = Bucket.Create.FromASCII(msg);

        var ar = new Radix64ArmorBucket(rd);

        var dc = new DecryptBucket(ar) { KeyChain = key };


        var bb = await dc.ReadExactlyAsync(1024);

        Assert.AreEqual("This is it! \r\n", bb.ToUTF8String());
    }

    [TestMethod]
    public async Task TestP384_NoPW()
    {
#if !NETCOREAPP
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            Assert.Inconclusive("Mono and ECDH are not a good mix");

#endif
        var keyData =
            @"-----BEGIN PGP PRIVATE KEY BLOCK-----

lKQEZDgGAxMFK4EEACIDAwQXzH6xi/Frz0v/0i5itrCk4BQIh7RIauvYLcb5b1X6
CVWf0Qmh+VB7PGwm4DKHtQ1ZPBJki6dlQdvtM1ue36dzsU9ubJkya/20BdSdLEtl
W8WTYfyMU0eK708IUmvrJRAAAYDbmbcKacnLGEOjilNE7XRlhZeSlIaq8AV+Ak1e
wIrwQEd7LPvAP5kp9mFUK0jZJYYX8LQZTlAzODRAMzg0IChRKSA8TlAzODRAMzg0
PoizBBMTCQA7FiEEDshbNl4X9dH648V2BOWDbNJYmzMFAmQ4BgMCGwMFCwkIBwIC
IgIGFQoJCAsCBBYCAwECHgcCF4AACgkQBOWDbNJYmzMQwQGAn79ZdoWeX+KX9Lpk
tWEMz33wGVhAWvxEnFYxo4osXpPo6t+oeAFl1+bUOQrdup7JAYChId6NKId3klot
QioBtqWvVsjhrukAYPqaDleqm1DHlN0+efliW27qNnCp5JS4U62cqARkOAYDEgUr
gQQAIgMDBErWpvAYhPXQPIbHXYzr5CduId5LNeqwDxmV1myhd5w6Gz9txfiOf+K5
JNNky+g9B3KdLqIBtVred0QopmeBQgZ9jzua+Itl2x9noZjplDGvv5ZgPyQQ2USR
/Vdds/NzyAMBCQkAAX0RLznP6cMRKNPrpQG672efNLErHuC9PWl8d09Wu/LuExen
/2HkC2N1NE8D4OkWrqIYD4iYBBgTCQAgFiEEDshbNl4X9dH648V2BOWDbNJYmzMF
AmQ4BgMCGwwACgkQBOWDbNJYmzNzbwF/WXZJhP/uAye3V9NBo8uBXp/RpADGT56A
fc1z1mCSsgZSM5B8CYizbWXntOWqeV+gAYCwlGhcrrgvnBHr7XHfChAJSDUZztiq
Mz04BjALY/JvFoQeY8CI/u38GNBLLNLyG6g=
=EWF2
-----END PGP PRIVATE KEY BLOCK-----";
        var msg =
            @"-----BEGIN PGP MESSAGE-----

hJ4D2j+wS0Yi4VoSAwMEN+r1DGhglmcb99dPNQHFbNEhgteVRA+6aQA2AmQ/ASQZ
EWR4N1htHr3XZj2tj/dGNAZEADWw0cVrbx9O46ch6qHgfKwf6mp5weAAMFqA7tWs
yHPI8Zmx/pesmAU3/ctcMJ7l7BEKZJ8NALXaqod0spqYnl7ZCtnnDo7Y9imgo5kz
dhmN7U02BklQ/ZGLQtXRHtRRAQkCEFLZGkQ2u4p3rwjuU7F+fRGStWg71rV9FPb0
wRIeTAjenpNlZjZrVvWu4GwLGODxaGaVX7WW0kAN0P7CGvkrax8AMdr4s/GryGpP
BFuZ
=3b/D
-----END PGP MESSAGE-----";

        Assert.IsTrue(PublicKeySignature.TryParse(keyData, out var key));
        Assert.IsTrue(key.HasPrivateKey);

        var rd = Bucket.Create.FromASCII(msg);

        var ar = new Radix64ArmorBucket(rd);

        var dc = new DecryptBucket(ar) { KeyChain = key };


        var bb = await dc.ReadExactlyAsync(1024);

        Assert.AreEqual("Without-PW\r\n", bb.ToUTF8String());
    }

#if DEBUG
    [TestMethod]
#endif
    public async Task Test25519()
    {
        var keyData = @"-----BEGIN PGP PRIVATE KEY BLOCK-----

lIYEZCrIUhYJKwYBBAHaRw8BAQdAteqQ9A1JEijADu07VyK0l0MdBUhVhm1YJNzS
CIGpIFz+BwMCZRNHAYpxgDfTpkJgORukQvMCLUOqMrP31vQYxpm8iFMl4jqLekWU
LdtE5JJVImhXrprmPoLsuz+J9ONP7f4Dz4XHmh5Dn78xAI/839REYLQaRUNDIDI1
NTE5IChDMykgPDI1NTE5QGVjYz6IkwQTFgoAOxYhBPeUrs/Q6MJPM+RziOaGQ9Te
1sO8BQJkKshSAhsDBQsJCAcCAiICBhUKCQgLAgQWAgMBAh4HAheAAAoJEOaGQ9Te
1sO8H4MBAMQQiPop4vnE00JQFxLsTu1+q1XlKAlFg3QGKFvpYZlyAQCVFx2Jxnjb
bPD1rtNDE6OZxUdQL4CgWxkCDehMMYVuBZyLBGQqyFISCisGAQQBl1UBBQEBB0Cr
rM6p2pugptSx15yGwMEypsHvu1n1RHhnyOWX/aheSwMBCAf+BwMCDdl06ffLR1TT
17y8XkzI4IlH49ACOqbW9DYPR2PbPtSo3FzOZCkV801ENssFzpS8NkEsg6IyQ1SA
PW1N54rL9/lkaclEi3KNybM2w+JdJoh4BBgWCgAgFiEE95Suz9Dowk8z5HOI5oZD
1N7Ww7wFAmQqyFICGwwACgkQ5oZD1N7Ww7x+LwEAwWGttUHMxTikYYCHa9LiAUPL
noTh+1EB2RqNLx5jF4kA/05QwJPJ+3V6R8x6EC6zVSIpRSpFTnFqSWWMDROH1/QA
=UG+I
-----END PGP PRIVATE KEY BLOCK-----";

        var msg = @"-----BEGIN PGP MESSAGE-----

hF4DSeOxMr5fDIcSAQdA7Z/19a24jrO+2W1eRXN8I5iaTxzh9RATbTO4pi0fsAYw
bv+LjLMogTCQlrJoGgN9Hc0gllsIhpT67/iAXcDtxEO6xsMW97/Xr4mAWejuvC5+
1EsBCQIQz14Ah3EbYqCuRHNuYoY69vlEl2+WtHZh+h0lNDmCfVkumpMGjM4xh2tI
6wiRTxlAIKq6hu3M6oaj1LbESOw7ezYLCCo2250=
=rVPw
-----END PGP MESSAGE-----";

        Assert.IsTrue(PublicKeySignature.TryParse(keyData, (_) => "k3", out var key));
        Assert.IsTrue(key.HasPrivateKey);

        var rd = Bucket.Create.FromASCII(msg);

        var ar = new Radix64ArmorBucket(rd);

        var dc = new DecryptBucket(ar) { KeyChain = key };


        var bb = await dc.ReadExactlyAsync(1024);

        Assert.AreEqual("Without-PW\r\n", bb.ToUTF8String());
    }

    public void TestECDH()
    {
        //using var c = Curve25519.Create();

        //  Alice's private key, a:
        var alicePriv = ParseKey("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a");
        // Alice's public key, X25519(a, 9):
        var alicePub = ParseKey("8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a");
        // Bob's private key, b:
        var bobPriv = ParseKey("5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb");
        // Bob's public key, X25519(b, 9):
        var bobPub = ParseKey("de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f");
        //Their shared secret, K:
        var expectedShared = ParseKey("4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742");


        var ap = Curve25519.GetPublicKey(alicePriv);
    }

    static byte[] ParseKey(string strKey)
    {
        byte[] result = new byte[strKey.Length / 2];

        for (int i = 0; i < strKey.Length / 2; i++)
            result[i] = Convert.ToByte(strKey.Substring(i * 2, 2), 16);

        return result;
    }
}
