using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using AmpScm.Buckets;
using AmpScm.Buckets.Cryptography;
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
    new("DSA/Elagamel", "k5",
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
                break; // Work
#if DEBUG
            case CryptoAlgorithm.Dsa:
            case CryptoAlgorithm.Ecdsa:
#endif
                break; // Should work

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

        var dc = new DecryptBucket(ar) { KeyChain = k1 + k2};


        var bb = await dc.ReadExactlyAsync(1024);

        Assert.AreEqual("\"Wow!\" \r\n", bb.ToUTF8String());
    }

#if DEBUG
    [TestMethod]
    public async Task TestP384()
    {
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
    public async Task testP384_NoPW()
    {
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
#endif
}

/*
gpg: reading options from '[cmdline]'
gpg: enabled debug flags: packet mpi crypto filter iobuf memory cache memstat trust hashing ipc clock lookup extprog
gpg: enabled compatibility flags:
gpg: DBG: [no clock] start
gpg: DBG: iobuf-1.0: open '[stdin]' desc=file_filter(fd) fd=776
gpg: DBG: iobuf-1.0: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.0: underflow: A->FILTER (65536 bytes)
gpg: DBG: iobuf-1.0: A->FILTER() returned rc=0 (ok), read 399 bytes
gpg: DBG: armor-filter: control: 5
gpg: DBG: iobuf-1.1: push 'armor_filter'
gpg: DBG: armor-filter: control: 5
gpg: DBG: iobuf chain: 1.1 'armor_filter' filter_eof=0 start=0 len=0
gpg: DBG: iobuf chain: 1.0 'file_filter(fd)' filter_eof=0 start=0 len=399
gpg: DBG: armor-filter: control: 1
gpg: DBG: iobuf-1.1: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.1: underflow: A->FILTER (65536 bytes)
gpg: DBG: armor-filter: control: 3
gpg: armor: BEGIN PGP MESSAGE
gpg: DBG: iobuf-1.1: A->FILTER() returned rc=0 (ok), read 243 bytes
gpg: DBG: parse_packet(iob=1): type=1 length=158 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/mainproc.c.1641)
# off=0 ctb=84 tag=1 hlen=2 plen=158
:pubkey enc packet: version 3, algo 18, keyid DA3FB04B4622E15A
	data: 0437EAF50C686096671BF7D74F3501C56CD12182D795440FBA69003602643F01241911647837586D1EBDD7663DAD8FF7463406440035B0D1C56B6F1F4EE3A721EAA1E07CAC1FEA6A79C1E000305A80EED5ACC873C8F199B1FE97AC980537FDCB5C
	data: 309EE5EC110A649F0D00B5DAAA8774B29A989E5ED90AD9E70E8ED8F629A0A3993376198DED4D36064950FD918B42D5D11E
gpg: public key is DA3FB04B4622E15A
gpg: DBG: free_packet() type=1
gpg: DBG: parse_packet(iob=1): type=20 length=81 (new_ctb) (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/mainproc.c.1641)
# off=160 ctb=d4 tag=20 hlen=2 plen=81 new-ctb
:aead encrypted packet: cipher=9 aead=2 cb=16
	length: 81
gpg: DBG: [no clock] keydb_new
gpg: DBG: [no clock] keydb_search enter
gpg: DBG: keydb_search: 1 search descriptions:
gpg: DBG: keydb_search   0: LONG_KID: 'DA3FB04B4622E15A'
gpg: DBG: keydb: kid_not_found_p (da3fb04b4622e15a) => indeterminate
gpg: DBG: internal_keydb_search: searching keybox (resource 0 of 1)
gpg: DBG: internal_keydb_search: searched keybox (resource 0 of 1) => Succes
gpg: DBG: [no clock] keydb_search leave (found)
gpg: DBG: [no clock] keydb_get_keyblock enter
gpg: DBG: parse_packet(iob=2): type=6 length=111 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=2): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=2): type=13 length=25 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=2): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=2): type=2 length=179 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=2): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=2): type=14 length=115 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=2): type=2 length=152 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=2): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: iobuf-2.0: underflow: buffer size: 636; still buffered: 0 => space for 636 bytes
gpg: DBG: iobuf-2.0: close '?'
gpg: DBG: [no clock] keydb_get_keyblock leave
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: NIST P-384
gpg: DBG: ecc_verify    p:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000ffffffff
gpg: DBG: ecc_verify    a:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000fffffffc
gpg: DBG: ecc_verify    b:+b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875a \
gpg: DBG:                  c656398d8a2ed19d2a85c8edd3ec2aef
gpg: DBG: ecc_verify  g.X:+aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a38 \
gpg: DBG:                  5502f25dbf55296c3a545e3872760ab7
gpg: DBG: ecc_verify  g.Y:+3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c0 \
gpg: DBG:                  0a60b1ce1d7e819d7a431d7c90ea0e5f
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf \
gpg: DBG:                  581a0db248b0a77aecec196accc52973
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [776 bit]
gpg: DBG:                  0417cc7eb18bf16bcf4bffd22e62b6b0a4e0140887b4486aebd82dc6f96f55fa \
gpg: DBG:                  09559fd109a1f9507b3c6c26e03287b50d593c12648ba76541dbed335b9edfa7 \
gpg: DBG:                  73b14f6e6c99326bfdb405d49d2c4b655bc59361fc8c53478aef4f08526beb25 \
gpg: DBG:                  10
gpg: DBG: ecc_verify data:+10c12839110658c481c185e4d5a49ff26adac4da5c8e6c4fda3501cb892a9f6e \
gpg: DBG:                  6e1079de00e2388da3f514195dae58c8
gpg: DBG: ecc_verify  s_r:+9fbf5976859e5fe297f4ba64b5610ccf7df01958405afc449c5631a38a2c5e93 \
gpg: DBG:                  e8eadfa8780165d7e6d4390addba9ec9
gpg: DBG: ecc_verify  s_s:+a121de8d288777925a2d422a01b6a5af56c8e1aee90060fa9a0e57aa9b50c794 \
gpg: DBG:                  dd3e79f9625b6eea3670a9e494b853ad
gpg: DBG: ecc_verify    => Good
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: NIST P-384
gpg: DBG: ecc_verify    p:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000ffffffff
gpg: DBG: ecc_verify    a:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000fffffffc
gpg: DBG: ecc_verify    b:+b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875a \
gpg: DBG:                  c656398d8a2ed19d2a85c8edd3ec2aef
gpg: DBG: ecc_verify  g.X:+aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a38 \
gpg: DBG:                  5502f25dbf55296c3a545e3872760ab7
gpg: DBG: ecc_verify  g.Y:+3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c0 \
gpg: DBG:                  0a60b1ce1d7e819d7a431d7c90ea0e5f
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf \
gpg: DBG:                  581a0db248b0a77aecec196accc52973
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [776 bit]
gpg: DBG:                  0417cc7eb18bf16bcf4bffd22e62b6b0a4e0140887b4486aebd82dc6f96f55fa \
gpg: DBG:                  09559fd109a1f9507b3c6c26e03287b50d593c12648ba76541dbed335b9edfa7 \
gpg: DBG:                  73b14f6e6c99326bfdb405d49d2c4b655bc59361fc8c53478aef4f08526beb25 \
gpg: DBG:                  10
gpg: DBG: ecc_verify data:+736ffab7414bc0fffc517ea63180ba4d5e464861e9e2ad84e36c32e0718dd434 \
gpg: DBG:                  ae1dcd5c23d558dbb6dacd613a9eb416
gpg: DBG: ecc_verify  s_r:+59764984ffee0327b757d341a3cb815e9fd1a400c64f9e807dcd73d66092b206 \
gpg: DBG:                  5233907c0988b36d65e7b4e5aa795fa0
gpg: DBG: ecc_verify  s_s:+b094685caeb82f9c11ebed71df0a1009483519ced8aa333d3806300b63f26f16 \
gpg: DBG:                  841e63c088feedfc18d04b2cd2f21ba8
gpg: DBG: ecc_verify    => Good
gpg: DBG: finish_lookup: checking key D2589B33 (one)(req_usage=0)
gpg: DBG: 	using key 4622E15A
gpg: using subkey DA3FB04B4622E15A instead of primary key 04E5836CD2589B33
gpg: DBG: [no clock] keydb_release
gpg: DBG: free_packet() type=6
gpg: DBG: free_packet() type=13
gpg: DBG: free_packet() type=2
gpg: DBG: free_packet() type=14
gpg: DBG: free_packet() type=2
gpg: encrypted with nistp384 key, ID DA3FB04B4622E15A, created 2023-04-13
      "NP384@384 (Q) <NP384@384>"
gpg: DBG: [no clock] get_session_key enter
gpg: DBG: chan_0x000000e4 <- OK Pleased to meet you
gpg: DBG: connection to the gpg-agent established
gpg: DBG: chan_0x000000e4 -> RESET
gpg: DBG: chan_0x000000e4 <- OK
gpg: DBG: chan_0x000000e4 -> GETINFO version
gpg: DBG: chan_0x000000e4 <- D 2.4.0
gpg: DBG: chan_0x000000e4 <- OK
gpg: DBG: chan_0x000000e4 -> OPTION allow-pinentry-notify
gpg: DBG: chan_0x000000e4 <- OK
gpg: DBG: chan_0x000000e4 -> OPTION agent-awareness=2.1.0
gpg: DBG: chan_0x000000e4 <- OK
gpg: DBG: chan_0x000000e4 -> SCD SERIALNO
gpg: DBG: chan_0x000000e4 <- ERR 100696144 No such device <SCD>
gpg: DBG: [no clock] keydb_new
gpg: DBG: [no clock] keydb_search enter
gpg: DBG: keydb_search: 1 search descriptions:
gpg: DBG: keydb_search   0: FIRST
gpg: DBG: internal_keydb_search: searching keybox (resource 0 of 1)
gpg: DBG: internal_keydb_search: searched keybox (resource 0 of 1) => Succes
gpg: DBG: [no clock] keydb_search leave (found)
gpg: DBG: [no clock] keydb_get_keyblock enter
gpg: DBG: parse_packet(iob=3): type=6 length=141 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=3): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=3): type=13 length=26 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=3): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=3): type=2 length=209 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=3): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=3): type=14 length=141 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=3): type=2 length=182 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=3): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: iobuf-3.0: underflow: buffer size: 753; still buffered: 0 => space for 753 bytes
gpg: DBG: iobuf-3.0: close '?'
gpg: DBG: [no clock] keydb_get_keyblock leave
gpg: DBG: chan_0x000000e4 -> HAVEKEY --list=1000
gpg: DBG: chan_000000E4 <- [ 44 20 1b f3 65 85 a2 0b b9 46 eb a0 5a ab c9 b9 ...(298 byte(s) skipped) ]
gpg: DBG: chan_0x000000e4 <- OK
gpg: DBG: get_keygrip for public key
gpg: DBG: keygrip= b86f9e17741960dc1187149a39c5a06dfe1c624a
gpg: DBG: rsa_verify data:+01ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffff003031300d06096086480165030402010500042059 \
gpg: DBG:                  518540de4c1e1a00b1ee916d9f3d19e0d7019dc67f07bd1121bc87cb829f32
gpg: DBG: rsa_verify  sig:+333f04ba0f4f3da72c2b1ccf715d65d82170e6bc762aeb015a6a4ebf7f95a5da \
gpg: DBG:                  010333bf04cf3e430d85838c21f50c1008c287fa6211bbc181de3cf757c46da4 \
gpg: DBG:                  652a7eb1a0a19b098e0f1163bec05df4a3fc49a4183b63d120c6bd09bea42d58 \
gpg: DBG:                  ad4f71ae1ae246a5aa76a7c0b5d53cac0acc3ac270497d003b0060245f9b91c1
gpg: DBG: rsa_verify    n:+e25e41adce8585b78a4a5cbd8b86caa2e8a71a0cedb875053a3b16c80113c0a4 \
gpg: DBG:                  7461c4f269ea6bb60e1a92fd6b3541b06ea817e81b70677885e2a9c6e1c851fc \
gpg: DBG:                  5c19806f74939d7ed841d075c986bbe1db6ac7b5cad1b556d51046aaac4a69f6 \
gpg: DBG:                  8eca88119f9dadf99b35327a3c91823249bda96a19157335b227d241be8ffcf1
gpg: DBG: rsa_verify    e:+010001
gpg: DBG: rsa_verify  cmp:+01ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffff003031300d06096086480165030402010500042059 \
gpg: DBG:                  518540de4c1e1a00b1ee916d9f3d19e0d7019dc67f07bd1121bc87cb829f32
gpg: DBG: rsa_verify    => Good
gpg: DBG: rsa_verify data:+01ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffff003031300d060960864801650304020105000420b1 \
gpg: DBG:                  b68228e51ba04018878a43de9517fe8412acbd458f93031affe36706677fc6
gpg: DBG: rsa_verify  sig:+942b09948b405e09394f43e89c6d7f91294883efac428512fb8f16f7635f5c9a \
gpg: DBG:                  ad88cd902464ba60b2ae2511dbd895db54ee5ef39000425fa3ea8a556fcff891 \
gpg: DBG:                  7865bfb4a7291ed6b84a15ddf5f7d0602931664d6441d4833e020e9d7e86cec6 \
gpg: DBG:                  4c7a3ca358ae91d954b96c73a9a0eb8b20fff48b29150d4f917083990b4c45c4
gpg: DBG: rsa_verify    n:+e25e41adce8585b78a4a5cbd8b86caa2e8a71a0cedb875053a3b16c80113c0a4 \
gpg: DBG:                  7461c4f269ea6bb60e1a92fd6b3541b06ea817e81b70677885e2a9c6e1c851fc \
gpg: DBG:                  5c19806f74939d7ed841d075c986bbe1db6ac7b5cad1b556d51046aaac4a69f6 \
gpg: DBG:                  8eca88119f9dadf99b35327a3c91823249bda96a19157335b227d241be8ffcf1
gpg: DBG: rsa_verify    e:+010001
gpg: DBG: rsa_verify  cmp:+01ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffff003031300d060960864801650304020105000420b1 \
gpg: DBG:                  b68228e51ba04018878a43de9517fe8412acbd458f93031affe36706677fc6
gpg: DBG: rsa_verify    => Good
gpg: DBG: finish_lookup: checking key 49EC7458 (all)(req_usage=0)
gpg: DBG: 	using key 49EC7458
gpg: DBG: free_packet() type=6
gpg: DBG: free_packet() type=13
gpg: DBG: free_packet() type=2
gpg: DBG: free_packet() type=14
gpg: DBG: free_packet() type=2
gpg: DBG: [no clock] keydb_search enter
gpg: DBG: keydb_search: 1 search descriptions:
gpg: DBG: keydb_search   0: NEXT
gpg: DBG: internal_keydb_search: searching keybox (resource 0 of 1)
gpg: DBG: internal_keydb_search: searched keybox (resource 0 of 1) => Succes
gpg: DBG: [no clock] keydb_search leave (found)
gpg: DBG: [no clock] keydb_get_keyblock enter
gpg: DBG: parse_packet(iob=4): type=6 length=525 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=4): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=4): type=13 length=24 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=4): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=4): type=2 length=593 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=4): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=4): type=14 length=525 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=4): type=2 length=566 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=4): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: iobuf-4.0: underflow: buffer size: 2291; still buffered: 0 => space for 2291 bytes
gpg: DBG: iobuf-4.0: close '?'
gpg: DBG: [no clock] keydb_get_keyblock leave
gpg: DBG: get_keygrip for public key
gpg: DBG: keygrip= 936ae8f116cdaa67b2e4a18cf67d66590730b18d
gpg: DBG: rsa_verify data:+01ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffff003031300d060960864801650304020105000420d2 \
gpg: DBG:                  45374ea0df4721d5f2f56e648df520938279f3b87f23edaa02c38bf3ca55db
gpg: DBG: rsa_verify  sig:+3e55a7b22455726f37771aeeecbd5ed39c4993a232dc22c4be4eaaf216c6fb3e \
gpg: DBG:                  ce5a4ba0ad9730674dfbb699fd21cca375cbf2bd718d0a4a142e30afde9baeee \
gpg: DBG:                  64fc9536120818592734187dcc1f8ea3f324766c82e381ffaf81de67597a3dde \
gpg: DBG:                  b8dda747be5746fe55dca41194d09f51a117a9dc2878abb0cf5e62b0bb2fbf11 \
gpg: DBG:                  c4d7d539599e21b95445189af9c5112ac054fb3501aa557b1d88bfbc9cfcdfd6 \
gpg: DBG:                  b3444708fc4360befb5dbddceeff26e10707bd7a8c7c80944dd217998d804443 \
gpg: DBG:                  08463c0e92a3642092795e076e7f529573f3f91495d2e4d9e5d3664e34f029ee \
gpg: DBG:                  08e45367f2084fade74333487350d1a95731bb3d057add864c7719d69cf1063f \
gpg: DBG:                  c1974af4ac918f5101d9cd9bf8b2d4af7d2e57d83f668440d4548600ab1528b5 \
gpg: DBG:                  aa05ef9dd4cad265fc4fc8320060b5069c6923095b1bad36cb0419bb1d7b20aa \
gpg: DBG:                  0054d5d547db32d585f09f2306c99b570c39cdc4d8f7b4a95a387e270c7a0ada \
gpg: DBG:                  1e17cfe094b1ef9d3a2f6403504adf9d1de270a1c140a91eb99ba7834053f5b7 \
gpg: DBG:                  34a7136d167d87b4d51e90a6f5651ec712e0b0fe07c0a63f3224dabb657128da \
gpg: DBG:                  fd71200f9c148ec7f1753b4899f90c2c2a85797fb3d0437bb013f0bfa9eab673 \
gpg: DBG:                  22667b5ab06f243f51078b6f4768db456c72cb9bd0ba786fe8dbc0e2b5719471 \
gpg: DBG:                  860529d63d29ad36526ba77eada20b0938372a9c526c7b4a97d1235957338d87
gpg: DBG: rsa_verify    n:+a6e0344b2e4e7dd5d245795237f6a72e2481f380c9da6bc4fb44e07cbcbc7aa6 \
gpg: DBG:                  2cdebb6967ac9803a55239f9714d0b6171e15175897551238d0b636aa1ea3e53 \
gpg: DBG:                  dd6d67f6848d3cf3f006ef4fe4c69b81e714b425467a068532304e7634e3574a \
gpg: DBG:                  fac9dc459e0e1d70c6c0c2e25d519d3896f7fa0179bd322035cf53fd76364470 \
gpg: DBG:                  eb04f55815348b91e0f6242164f9901348e9379b01954f5c3f96e9ca462ebc22 \
gpg: DBG:                  7208fb607eb2f4f84d144db9890a5baa41b77cc36634fae91a7d891cc13e2d23 \
gpg: DBG:                  bb1d3b183d04ed72930e651d61424af8aafdf07639a286952e2533656dd33b3f \
gpg: DBG:                  e56d7a2885dcbe028bff688faec22e91ec47abbb24edd39fd18a61682cdb6366 \
gpg: DBG:                  5ffbe0174bc4bd2ed4acacff762a20940aa2f43cc2cccc95a74fed479463271f \
gpg: DBG:                  44f126ef872a2a8eb52de1f0cb2190db849a64f0692e705b206bdc4f5bc3c37c \
gpg: DBG:                  8836e64068331d6929a2e91608d40f1adffb74ff03143b562430cab9e925d7f5 \
gpg: DBG:                  25746c440a27e2201db6edd40243b97ae1315dded0a1c102bf88c2eef1ec66b0 \
gpg: DBG:                  ef38877ba0ad7bcc655aa3e59177b9a4608f02f9cc723cc8c23dcd9e980a2a30 \
gpg: DBG:                  c60aa4a8f0e79f57d47c1044c7ff4ee13663e73579ce2d936f86bc30d9a001cb \
gpg: DBG:                  dd87f9043c126c6275b96927fad1b8e54561a81576077ae3148de7122c440a37 \
gpg: DBG:                  ecfedf64d8da7942f1087b8ed2f63978e0b341c5930eaa24c75227650b7aa595
gpg: DBG: rsa_verify    e:+010001
gpg: DBG: rsa_verify  cmp:+01ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffff003031300d060960864801650304020105000420d2 \
gpg: DBG:                  45374ea0df4721d5f2f56e648df520938279f3b87f23edaa02c38bf3ca55db
gpg: DBG: rsa_verify    => Good
gpg: DBG: rsa_verify data:+01ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffff003031300d06096086480165030402010500042048 \
gpg: DBG:                  7703c22738dbcb965fce95631b787efe4f1a2727e549a4feb4dab3d4feff7b
gpg: DBG: rsa_verify  sig:+11bc3ba549f841de97e55399f38bbe72b5e4404b5a37f492f6a52e955fe10008 \
gpg: DBG:                  9ee86cafcc794689e4b5145823de3dbe57d10f945eece155a468e7508a96b577 \
gpg: DBG:                  d0ad51a898da9f61ca3e1418222d0b4a1e2e455828bc942066ddb11af831aedd \
gpg: DBG:                  221478875092ef78b061834ab85091e0cfaad3836893fb2cccae6dacaa116e68 \
gpg: DBG:                  707a3c685464d16c3b20fceb9e46d72e78d8eef20458a9396b9c7cc12126221e \
gpg: DBG:                  64a8f411b8eb7acd111e2ccc8f2023265fd063ee3b4ce1d570a73b4bd4bafe8b \
gpg: DBG:                  d01ce86df2406b31dd6fbf50d6fab72b6c3a012b34ef56284f8e177bc7fd12ba \
gpg: DBG:                  e84607bd0f111b586de2cf675db6343cc7e1d998d54a632214a798fa565b6eb6 \
gpg: DBG:                  63ada77c1690dfd619390d10a46b78177054011c138004d51325fd1e112a084d \
gpg: DBG:                  ef3389c00e7b59da1c475fa86cf7b2d5502e6e47eecfe9483951ca2e4ea19fb1 \
gpg: DBG:                  5b37346f29550331ef1d00f317399b4bc98a99a6de1d602e58d35057bf5fa712 \
gpg: DBG:                  3e7b17d28b97d7a312f0992f4a4464d69d4d673928d4bdf502e7b7b425ce1df3 \
gpg: DBG:                  a146839e3cfcadf726ab656e30a0cd7dd674d12b074ea54fbb32e73b412861e6 \
gpg: DBG:                  408d34ca87d71d59ebea57c585e3a1837b499d0ae38321194ba8d25e7a967b1e \
gpg: DBG:                  b3bbf9657b6fd9b8cbe39e3b3e48b7a574cd2972250491cb4f8053ea95b9ecea \
gpg: DBG:                  fe08b8209f53fbef072f4f8873e53400837440a4a4bbd08a1fd63a67e349e70f
gpg: DBG: rsa_verify    n:+a6e0344b2e4e7dd5d245795237f6a72e2481f380c9da6bc4fb44e07cbcbc7aa6 \
gpg: DBG:                  2cdebb6967ac9803a55239f9714d0b6171e15175897551238d0b636aa1ea3e53 \
gpg: DBG:                  dd6d67f6848d3cf3f006ef4fe4c69b81e714b425467a068532304e7634e3574a \
gpg: DBG:                  fac9dc459e0e1d70c6c0c2e25d519d3896f7fa0179bd322035cf53fd76364470 \
gpg: DBG:                  eb04f55815348b91e0f6242164f9901348e9379b01954f5c3f96e9ca462ebc22 \
gpg: DBG:                  7208fb607eb2f4f84d144db9890a5baa41b77cc36634fae91a7d891cc13e2d23 \
gpg: DBG:                  bb1d3b183d04ed72930e651d61424af8aafdf07639a286952e2533656dd33b3f \
gpg: DBG:                  e56d7a2885dcbe028bff688faec22e91ec47abbb24edd39fd18a61682cdb6366 \
gpg: DBG:                  5ffbe0174bc4bd2ed4acacff762a20940aa2f43cc2cccc95a74fed479463271f \
gpg: DBG:                  44f126ef872a2a8eb52de1f0cb2190db849a64f0692e705b206bdc4f5bc3c37c \
gpg: DBG:                  8836e64068331d6929a2e91608d40f1adffb74ff03143b562430cab9e925d7f5 \
gpg: DBG:                  25746c440a27e2201db6edd40243b97ae1315dded0a1c102bf88c2eef1ec66b0 \
gpg: DBG:                  ef38877ba0ad7bcc655aa3e59177b9a4608f02f9cc723cc8c23dcd9e980a2a30 \
gpg: DBG:                  c60aa4a8f0e79f57d47c1044c7ff4ee13663e73579ce2d936f86bc30d9a001cb \
gpg: DBG:                  dd87f9043c126c6275b96927fad1b8e54561a81576077ae3148de7122c440a37 \
gpg: DBG:                  ecfedf64d8da7942f1087b8ed2f63978e0b341c5930eaa24c75227650b7aa595
gpg: DBG: rsa_verify    e:+010001
gpg: DBG: rsa_verify  cmp:+01ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff \
gpg: DBG:                  ffffffffffffffffffffff003031300d06096086480165030402010500042048 \
gpg: DBG:                  7703c22738dbcb965fce95631b787efe4f1a2727e549a4feb4dab3d4feff7b
gpg: DBG: rsa_verify    => Good
gpg: DBG: finish_lookup: checking key 2E0DEB8F (all)(req_usage=0)
gpg: DBG: 	using key 2E0DEB8F
gpg: DBG: free_packet() type=6
gpg: DBG: free_packet() type=13
gpg: DBG: free_packet() type=2
gpg: DBG: free_packet() type=14
gpg: DBG: free_packet() type=2
gpg: DBG: [no clock] keydb_search enter
gpg: DBG: keydb_search: 1 search descriptions:
gpg: DBG: keydb_search   0: NEXT
gpg: DBG: internal_keydb_search: searching keybox (resource 0 of 1)
gpg: DBG: internal_keydb_search: searched keybox (resource 0 of 1) => Succes
gpg: DBG: [no clock] keydb_search leave (found)
gpg: DBG: [no clock] keydb_get_keyblock enter
gpg: DBG: parse_packet(iob=5): type=6 length=51 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=5): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=5): type=13 length=26 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=5): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=5): type=2 length=147 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=5): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=5): type=14 length=56 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=5): type=2 length=120 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=5): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: iobuf-5.0: underflow: buffer size: 454; still buffered: 0 => space for 454 bytes
gpg: DBG: iobuf-5.0: close '?'
gpg: DBG: [no clock] keydb_get_keyblock leave
gpg: DBG: get_keygrip for public key
gpg: DBG: keygrip= f3e24f98081d0292a1aecef2fea8be02eda1f6f6
gpg: DBG: ecc_verify info: Edwards/Ed25519+EdDSA
gpg: DBG: ecc_verify name: Ed25519
gpg: DBG: ecc_verify    p:+7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffed
gpg: DBG: ecc_verify    a:+7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffec
gpg: DBG: ecc_verify    b:+52036cee2b6ffe738cc740797779e89800700a4d4141d8ab75eb4dca135978a3
gpg: DBG: ecc_verify  g.X:+216936d3cd6e53fec0a4e231fdd6dc5c692cc7609525a7b2c9562d608f25d51a
gpg: DBG: ecc_verify  g.Y:+6666666666666666666666666666666666666666666666666666666666666658
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+1000000000000000000000000000000014def9dea2f79cd65812631a5cf5d3ed
gpg: DBG: ecc_verify    h:+08
gpg: DBG: ecc_verify    q: [264 bit]
gpg: DBG:                  40b5ea90f40d491228c00eed3b5722b497431d054855866d5824dcd20881a920 \
gpg: DBG:                  5c
gpg: DBG: ecc_verify data: [512 bit]
gpg: DBG:                  1f837dd2d8c025513835a101701cb8a49711844d9dc7d38e77f5e66de560616e \
gpg: DBG:                  d71871c6cd6486af245f1c184afe77e37aa9ca79c83106b49c68e7ddfbae2044
gpg: DBG: ecc_verify  s_r: [256 bit]
gpg: DBG:                  c41088fa29e2f9c4d342501712ec4eed7eab55e5280945837406285be9619972
gpg: DBG: ecc_verify  s_s: [256 bit]
gpg: DBG:                  95171d89c678db6cf0f5aed34313a399c547502f80a05b19020de84c31856e05
gpg: DBG:   e_pk: b5ea90f40d491228c00eed3b5722b497431d054855866d5824dcd20881a9205c
gpg: DBG:      m: 1f837dd2d8c025513835a101701cb8a49711844d9dc7d38e77f5e66de560616e \
gpg: DBG:         d71871c6cd6486af245f1c184afe77e37aa9ca79c83106b49c68e7ddfbae2044
gpg: DBG:      r: c41088fa29e2f9c4d342501712ec4eed7eab55e5280945837406285be9619972
gpg: DBG:  H(R+): 1505eb17a41030e08e5dc110460711a24b4bd8e40c09cb3cff5f556780e08123 \
gpg: DBG:         b000f5ea0021e4004cd99478e01d2e7a946052fb095e7c7f2c46948572770aa9
gpg: DBG:      s: 056e85314ce80d02195ba0802f5047c599a31343d3aef5f06cdb78c6891d1795
gpg: DBG: ecc_verify    => Good
gpg: DBG: ecc_verify info: Edwards/Ed25519+EdDSA
gpg: DBG: ecc_verify name: Ed25519
gpg: DBG: ecc_verify    p:+7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffed
gpg: DBG: ecc_verify    a:+7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffec
gpg: DBG: ecc_verify    b:+52036cee2b6ffe738cc740797779e89800700a4d4141d8ab75eb4dca135978a3
gpg: DBG: ecc_verify  g.X:+216936d3cd6e53fec0a4e231fdd6dc5c692cc7609525a7b2c9562d608f25d51a
gpg: DBG: ecc_verify  g.Y:+6666666666666666666666666666666666666666666666666666666666666658
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+1000000000000000000000000000000014def9dea2f79cd65812631a5cf5d3ed
gpg: DBG: ecc_verify    h:+08
gpg: DBG: ecc_verify    q: [264 bit]
gpg: DBG:                  40b5ea90f40d491228c00eed3b5722b497431d054855866d5824dcd20881a920 \
gpg: DBG:                  5c
gpg: DBG: ecc_verify data: [512 bit]
gpg: DBG:                  7e2fe492493823760ee81ca67b51e2efad265a82710ed05596c7949a080ac6e7 \
gpg: DBG:                  0ce573ae70bf2b583d06d09941addef83c2ab940eae5ee016742630d1d9a4097
gpg: DBG: ecc_verify  s_r: [256 bit]
gpg: DBG:                  c161adb541ccc538a46180876bd2e20143cb9e84e1fb5101d91a8d2f1e631789
gpg: DBG: ecc_verify  s_s: [256 bit]
gpg: DBG:                  4e50c093c9fb757a47cc7a102eb3552229452a454e716a49658c0d1387d7f400
gpg: DBG:   e_pk: b5ea90f40d491228c00eed3b5722b497431d054855866d5824dcd20881a9205c
gpg: DBG:      m: 7e2fe492493823760ee81ca67b51e2efad265a82710ed05596c7949a080ac6e7 \
gpg: DBG:         0ce573ae70bf2b583d06d09941addef83c2ab940eae5ee016742630d1d9a4097
gpg: DBG:      r: c161adb541ccc538a46180876bd2e20143cb9e84e1fb5101d91a8d2f1e631789
gpg: DBG:  H(R+): 9720221c5f9fe77a099390b806c18510d643e75cf8669a11cdced7fcf9f32bf2 \
gpg: DBG:         6ebe782d0fe35efb06e39a15e4c56454cda08bf9ae53609da5c8f0db60125cf0
gpg: DBG:      s: 00f4d787130d8c65496a714e452a45292255b32e107acc477a75fbc993c0504e
gpg: DBG: ecc_verify    => Good
gpg: DBG: finish_lookup: checking key DED6C3BC (all)(req_usage=0)
gpg: DBG: 	using key DED6C3BC
gpg: DBG: free_packet() type=6
gpg: DBG: free_packet() type=13
gpg: DBG: free_packet() type=2
gpg: DBG: free_packet() type=14
gpg: DBG: free_packet() type=2
gpg: DBG: [no clock] keydb_search enter
gpg: DBG: keydb_search: 1 search descriptions:
gpg: DBG: keydb_search   0: NEXT
gpg: DBG: internal_keydb_search: searching keybox (resource 0 of 1)
gpg: DBG: internal_keydb_search: searched keybox (resource 0 of 1) => Succes
gpg: DBG: [no clock] keydb_search leave (found)
gpg: DBG: [no clock] keydb_get_keyblock enter
gpg: DBG: parse_packet(iob=6): type=6 length=111 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=6): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=6): type=13 length=19 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=6): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=6): type=2 length=179 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=6): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: iobuf-6.0: underflow: buffer size: 351; still buffered: 0 => space for 351 bytes
gpg: DBG: iobuf-6.0: close '?'
gpg: DBG: [no clock] keydb_get_keyblock leave
gpg: DBG: get_keygrip for public key
gpg: DBG: keygrip= 6794d74d17f13abc12c5a9f575d235cf3b6dd692
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: NIST P-384
gpg: DBG: ecc_verify    p:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000ffffffff
gpg: DBG: ecc_verify    a:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000fffffffc
gpg: DBG: ecc_verify    b:+b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875a \
gpg: DBG:                  c656398d8a2ed19d2a85c8edd3ec2aef
gpg: DBG: ecc_verify  g.X:+aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a38 \
gpg: DBG:                  5502f25dbf55296c3a545e3872760ab7
gpg: DBG: ecc_verify  g.Y:+3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c0 \
gpg: DBG:                  0a60b1ce1d7e819d7a431d7c90ea0e5f
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf \
gpg: DBG:                  581a0db248b0a77aecec196accc52973
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [776 bit]
gpg: DBG:                  04a7a43f556a8c6815fe51849ae2c93329888663a956f3b517a1559321f44a76 \
gpg: DBG:                  30c80ce1c2b159bf24a212593c8f144ab24526bc8d01d775bc18127dad27bc12 \
gpg: DBG:                  c90ed5fe9fdfae09ab2bb6ccb29517d1df0672a4ffc061f69382d8a1eae8c4a8 \
gpg: DBG:                  ee
gpg: DBG: ecc_verify data:+4132f0d95708a226931441ce7ca26ce03484979c97442c6e98202e3556add93b \
gpg: DBG:                  765ab30aff91f284a1f207bcee7f626b
gpg: DBG: ecc_verify  s_r:+e68de5374eb0bd6374b1158405d351d17e5ac5a5920c82cc41803eeb3dc2810a \
gpg: DBG:                  01ae3031eb3fdf33d1daa930bc8d016e
gpg: DBG: ecc_verify  s_s:+57a782bc027f1d4d607c42c29dd57f053524bc0374ae324edb54ab91e70c9ae5 \
gpg: DBG:                  5a9b623fa048543bba52e39ccfee3108
gpg: DBG: ecc_verify    => Good
gpg: DBG: finish_lookup: checking key 25CA7A79 (all)(req_usage=0)
gpg: DBG: 	using key 25CA7A79
gpg: DBG: free_packet() type=6
gpg: DBG: free_packet() type=13
gpg: DBG: free_packet() type=2
gpg: DBG: [no clock] keydb_search enter
gpg: DBG: keydb_search: 1 search descriptions:
gpg: DBG: keydb_search   0: NEXT
gpg: DBG: internal_keydb_search: searching keybox (resource 0 of 1)
gpg: DBG: internal_keydb_search: searched keybox (resource 0 of 1) => Succes
gpg: DBG: [no clock] keydb_search leave (found)
gpg: DBG: [no clock] keydb_get_keyblock enter
gpg: DBG: parse_packet(iob=7): type=6 length=1198 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=7): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=7): type=13 length=24 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=7): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=7): type=2 length=147 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=7): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=7): type=14 length=781 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=7): type=2 length=120 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=7): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: iobuf-7.0: underflow: buffer size: 2326; still buffered: 0 => space for 2326 bytes
gpg: DBG: iobuf-7.0: close '?'
gpg: DBG: [no clock] keydb_get_keyblock leave
gpg: DBG: get_keygrip for public key
gpg: DBG: keygrip= 806923bdf27f9e3b30b7bccff58ade953811346d
gpg: DBG: dsa_verify data:+7e0c63be0b8bf6c0331a76d46679f6b41f8dd94262bf2ffd26a4ca24b5d104fb
gpg: DBG: dsa_verify  s_r:+91886fd0ae6d49eeebe6ae50a83377a9ce4cc08ad7c0172ee2298108cee509f5
gpg: DBG: dsa_verify  s_s:+3539e91beccfba669313a1bb2b2cce06f558f656402da6a97447828c50547faf
gpg: DBG: dsa_verify    p:+cc57db10bd4e5f06e1af3f17d4148427880f4a5a55b0ea62bdcf006fa377ba31 \
gpg: DBG:                  c3a77c211a26853a1e50176f990e4d187d6e9ccee9b8ec176bca7089fd871498 \
gpg: DBG:                  6410959c9c7f9f8deeb0adf40737917d008a1ec9dc3fcfcaa57b31124d79112a \
gpg: DBG:                  a5847a5278b9cd5e1e49182f1ba642207448a50a48a91991232c177ce6f46a37 \
gpg: DBG:                  8338fbb6004d027fb7018ec164e6686d6f33d07737bec3273549f4475c817d75 \
gpg: DBG:                  3212ea8dd5cfff07d48d071ae37b2104590b7fe9753980b169b254f62e83767f \
gpg: DBG:                  d9c4319844d37b801a17f143903cd66c5d848ce5673ec097b829cf3c4b6e045a \
gpg: DBG:                  49a20d92f2f98ebba96b6f926595aec1322bd88bb0cedbad1bbfe79721db0ad7 \
gpg: DBG:                  d6548d85d5244358b0f9dbc1507d29837bee667eea7f98133662e18442e75a4e \
gpg: DBG:                  f5feea664698174a881086355ce386d2d9cc0aaf2fff42d319551320f66bb87e \
gpg: DBG:                  cabd8936d43df94b8b1ea517676ee6c438ebf8c9b4287da526332a7af4b8bbca \
gpg: DBG:                  e630fa973325f061cfb8c4feaf946506e671098d6896f0edc22c3cc3aefe50e3
gpg: DBG: dsa_verify    q:+bbcf84930ca6d8848276ff70c5a6838bde9b960a21eb89e162ff59d2e30bec9d
gpg: DBG: dsa_verify    g:+8c78fe579a8f4005aecad2704669e28fb8439ef71eb46490a4dd874ec3b1f66e \
gpg: DBG:                  bafd0d35e2562de3d3464738057739c7119d83cfbef862cc49a4947768f57464 \
gpg: DBG:                  20cdc8496b5fbdc2f11046e85654c3a65d888764f9a52e5f9de631a22913f0ef \
gpg: DBG:                  0425c2a4fb6a103533d94c7eb931377bf761cb52b51f0ebc6920640c499b0c6b \
gpg: DBG:                  020c9e9306c1009c0e50829dc39339f819705e1e540f854d40974e3c2a392eb6 \
gpg: DBG:                  a1275e79b7eaf5a214b2562ffb1ada667922d2db23bf85a4178db7fb86042e76 \
gpg: DBG:                  e364a33112ab9d836957a8da0d93c8a44421b7c3d393802a377ea1f30c666309 \
gpg: DBG:                  29a9173f43ec6cc5d53bede4f10f8823ea9a78ee84a7b0ebd3aee384e562259e \
gpg: DBG:                  1ff462d28c128560c9d276826f6a7281fb0ffe4b52d1e56d2d94238fe5a7296f \
gpg: DBG:                  933652eb8b18ee8514c7f0d713326168ef1976acd1d782bbaa7d97d36d708c38 \
gpg: DBG:                  8bd67ade77954758d62b0801f879c232f9a126324936afe2b85dcd25aa008d02 \
gpg: DBG:                  638212d2cb416d42e675754fbacd395c3dd0aae0fc4dc2cf674ebfd3cedc6ed1
gpg: DBG: dsa_verify    y:+27cd2aa5cf8102cbfd5d8038fd19bff74c7151c7c46e4cdffc53b855f92aed5f \
gpg: DBG:                  2ddef3f7be0eed2ef60eb97a87c2b8868b7da5fbef69ca8d36a0bb7b43e89c0c \
gpg: DBG:                  16c9748c24160fd472d5e58bcd6f5197845096e6e782334abbac94b37cd4ab5f \
gpg: DBG:                  3f50df765a6cc9b81de8397dba6e268fd950c436f8d5aedf17f4359751c8bc8f \
gpg: DBG:                  224d002efbc8339cd2bc4e6c459313730ad2a6a7f56131487f6b863fb5226baa \
gpg: DBG:                  1a7187b7dedf52e85920702d6ac26bfd2f57b72a6ec7b9590e21a30bbec3f392 \
gpg: DBG:                  70b21da4c6bfbd1d273f0e8db2fa03d3b996b0c6120a94815415629599367af9 \
gpg: DBG:                  75709093a3e8c81a008c17598cdf999f5ec3e272f22a3d4c0af085397e08ade6 \
gpg: DBG:                  264ffe8f9fe4a73d04a6caacb49a4f49f5fab2be537d42520edd81844c04236a \
gpg: DBG:                  8811bfa11bf56a2c2d3812437f121b9a9183eea05eec817a74665aa1851fc9bb \
gpg: DBG:                  3132b004153f895f746e07ab3a8f39621c594474bd08621ae9ee04fe24cb96bf \
gpg: DBG:                  72fe85cef765bf6b5ad993d1771df61d4f52a22cabd3dda926fbd7f4b3e4697a
gpg: DBG: dsa_verify    => Good
gpg: DBG: dsa_verify data:+a2e02bc9af73fb39f16de724634b66b1a079ea0f33848ccaef63561e36d2c227
gpg: DBG: dsa_verify  s_r:+71eb370a40aa9266efd3bcdc3a560f0290feb276edf40d6e3c33f58817875238
gpg: DBG: dsa_verify  s_s:+78f7776fcba638f658e7ebcf11b59fa8c3de560daba93dba23d5c02734bc6483
gpg: DBG: dsa_verify    p:+cc57db10bd4e5f06e1af3f17d4148427880f4a5a55b0ea62bdcf006fa377ba31 \
gpg: DBG:                  c3a77c211a26853a1e50176f990e4d187d6e9ccee9b8ec176bca7089fd871498 \
gpg: DBG:                  6410959c9c7f9f8deeb0adf40737917d008a1ec9dc3fcfcaa57b31124d79112a \
gpg: DBG:                  a5847a5278b9cd5e1e49182f1ba642207448a50a48a91991232c177ce6f46a37 \
gpg: DBG:                  8338fbb6004d027fb7018ec164e6686d6f33d07737bec3273549f4475c817d75 \
gpg: DBG:                  3212ea8dd5cfff07d48d071ae37b2104590b7fe9753980b169b254f62e83767f \
gpg: DBG:                  d9c4319844d37b801a17f143903cd66c5d848ce5673ec097b829cf3c4b6e045a \
gpg: DBG:                  49a20d92f2f98ebba96b6f926595aec1322bd88bb0cedbad1bbfe79721db0ad7 \
gpg: DBG:                  d6548d85d5244358b0f9dbc1507d29837bee667eea7f98133662e18442e75a4e \
gpg: DBG:                  f5feea664698174a881086355ce386d2d9cc0aaf2fff42d319551320f66bb87e \
gpg: DBG:                  cabd8936d43df94b8b1ea517676ee6c438ebf8c9b4287da526332a7af4b8bbca \
gpg: DBG:                  e630fa973325f061cfb8c4feaf946506e671098d6896f0edc22c3cc3aefe50e3
gpg: DBG: dsa_verify    q:+bbcf84930ca6d8848276ff70c5a6838bde9b960a21eb89e162ff59d2e30bec9d
gpg: DBG: dsa_verify    g:+8c78fe579a8f4005aecad2704669e28fb8439ef71eb46490a4dd874ec3b1f66e \
gpg: DBG:                  bafd0d35e2562de3d3464738057739c7119d83cfbef862cc49a4947768f57464 \
gpg: DBG:                  20cdc8496b5fbdc2f11046e85654c3a65d888764f9a52e5f9de631a22913f0ef \
gpg: DBG:                  0425c2a4fb6a103533d94c7eb931377bf761cb52b51f0ebc6920640c499b0c6b \
gpg: DBG:                  020c9e9306c1009c0e50829dc39339f819705e1e540f854d40974e3c2a392eb6 \
gpg: DBG:                  a1275e79b7eaf5a214b2562ffb1ada667922d2db23bf85a4178db7fb86042e76 \
gpg: DBG:                  e364a33112ab9d836957a8da0d93c8a44421b7c3d393802a377ea1f30c666309 \
gpg: DBG:                  29a9173f43ec6cc5d53bede4f10f8823ea9a78ee84a7b0ebd3aee384e562259e \
gpg: DBG:                  1ff462d28c128560c9d276826f6a7281fb0ffe4b52d1e56d2d94238fe5a7296f \
gpg: DBG:                  933652eb8b18ee8514c7f0d713326168ef1976acd1d782bbaa7d97d36d708c38 \
gpg: DBG:                  8bd67ade77954758d62b0801f879c232f9a126324936afe2b85dcd25aa008d02 \
gpg: DBG:                  638212d2cb416d42e675754fbacd395c3dd0aae0fc4dc2cf674ebfd3cedc6ed1
gpg: DBG: dsa_verify    y:+27cd2aa5cf8102cbfd5d8038fd19bff74c7151c7c46e4cdffc53b855f92aed5f \
gpg: DBG:                  2ddef3f7be0eed2ef60eb97a87c2b8868b7da5fbef69ca8d36a0bb7b43e89c0c \
gpg: DBG:                  16c9748c24160fd472d5e58bcd6f5197845096e6e782334abbac94b37cd4ab5f \
gpg: DBG:                  3f50df765a6cc9b81de8397dba6e268fd950c436f8d5aedf17f4359751c8bc8f \
gpg: DBG:                  224d002efbc8339cd2bc4e6c459313730ad2a6a7f56131487f6b863fb5226baa \
gpg: DBG:                  1a7187b7dedf52e85920702d6ac26bfd2f57b72a6ec7b9590e21a30bbec3f392 \
gpg: DBG:                  70b21da4c6bfbd1d273f0e8db2fa03d3b996b0c6120a94815415629599367af9 \
gpg: DBG:                  75709093a3e8c81a008c17598cdf999f5ec3e272f22a3d4c0af085397e08ade6 \
gpg: DBG:                  264ffe8f9fe4a73d04a6caacb49a4f49f5fab2be537d42520edd81844c04236a \
gpg: DBG:                  8811bfa11bf56a2c2d3812437f121b9a9183eea05eec817a74665aa1851fc9bb \
gpg: DBG:                  3132b004153f895f746e07ab3a8f39621c594474bd08621ae9ee04fe24cb96bf \
gpg: DBG:                  72fe85cef765bf6b5ad993d1771df61d4f52a22cabd3dda926fbd7f4b3e4697a
gpg: DBG: dsa_verify    => Good
gpg: DBG: finish_lookup: checking key 45F40A79 (all)(req_usage=0)
gpg: DBG: 	using key 45F40A79
gpg: DBG: free_packet() type=6
gpg: DBG: free_packet() type=13
gpg: DBG: free_packet() type=2
gpg: DBG: free_packet() type=14
gpg: DBG: free_packet() type=2
gpg: DBG: [no clock] keydb_search enter
gpg: DBG: keydb_search: 1 search descriptions:
gpg: DBG: keydb_search   0: NEXT
gpg: DBG: internal_keydb_search: searching keybox (resource 0 of 1)
gpg: DBG: internal_keydb_search: searched keybox (resource 0 of 1) => Succes
gpg: DBG: [no clock] keydb_search leave (found)
gpg: DBG: [no clock] keydb_get_keyblock enter
gpg: DBG: parse_packet(iob=8): type=6 length=83 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=8): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=8): type=13 length=28 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=8): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=8): type=2 length=147 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=8): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=8): type=14 length=87 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=8): type=2 length=120 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=8): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: iobuf-8.0: underflow: buffer size: 519; still buffered: 0 => space for 519 bytes
gpg: DBG: iobuf-8.0: close '?'
gpg: DBG: [no clock] keydb_get_keyblock leave
gpg: DBG: get_keygrip for public key
gpg: DBG: keygrip= 1bf36585a20bb946eba05aabc9b916eaef8d68d2
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: brainpoolP256r1
gpg: DBG: ecc_verify    p:+a9fb57dba1eea9bc3e660a909d838d726e3bf623d52620282013481d1f6e5377
gpg: DBG: ecc_verify    a:+7d5a0975fc2c3057eef67530417affe7fb8055c126dc5c6ce94a4b44f330b5d9
gpg: DBG: ecc_verify    b:+26dc5c6ce94a4b44f330b5d9bbd77cbf958416295cf7e1ce6bccdc18ff8c07b6
gpg: DBG: ecc_verify  g.X:+8bd2aeb9cb7e57cb2c4b482ffc81b7afb9de27e1e3bd23c23a4453bd9ace3262
gpg: DBG: ecc_verify  g.Y:+547ef835c3dac4fd97f8461a14611dc9c27745132ded8e545c1d54c72f046997
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+a9fb57dba1eea9bc3e660a909d838d718c397aa3b561a6f7901e0e82974856a7
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [520 bit]
gpg: DBG:                  041980a519268563e13f1f71655599f56388cf37360ce20ad323092c96723441 \
gpg: DBG:                  bd18680c62e18796d2a66e4471fc58072f780b88f154b4ffc8ca6e4fd9b88da4 \
gpg: DBG:                  fb
gpg: DBG: ecc_verify data:+e56a4d5dbb712fc7a2229b5bc350a4d7d413041ee5b052e8c902f8dc3fe63dad
gpg: DBG: ecc_verify  s_r:+1d54b892fdeffa62bedaea1d4fe75281e400cf636f44d28b1cfbcad852d72f51
gpg: DBG: ecc_verify  s_s:+56b2cccd7eb54dffcf3c0153b71c5bb64d3a24556be19d6be2622cae5cc8d0df
gpg: DBG: ecc_verify    => Good
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: brainpoolP256r1
gpg: DBG: ecc_verify    p:+a9fb57dba1eea9bc3e660a909d838d726e3bf623d52620282013481d1f6e5377
gpg: DBG: ecc_verify    a:+7d5a0975fc2c3057eef67530417affe7fb8055c126dc5c6ce94a4b44f330b5d9
gpg: DBG: ecc_verify    b:+26dc5c6ce94a4b44f330b5d9bbd77cbf958416295cf7e1ce6bccdc18ff8c07b6
gpg: DBG: ecc_verify  g.X:+8bd2aeb9cb7e57cb2c4b482ffc81b7afb9de27e1e3bd23c23a4453bd9ace3262
gpg: DBG: ecc_verify  g.Y:+547ef835c3dac4fd97f8461a14611dc9c27745132ded8e545c1d54c72f046997
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+a9fb57dba1eea9bc3e660a909d838d718c397aa3b561a6f7901e0e82974856a7
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [520 bit]
gpg: DBG:                  041980a519268563e13f1f71655599f56388cf37360ce20ad323092c96723441 \
gpg: DBG:                  bd18680c62e18796d2a66e4471fc58072f780b88f154b4ffc8ca6e4fd9b88da4 \
gpg: DBG:                  fb
gpg: DBG: ecc_verify data:+324dbbe144bd53058b50c207a67153c7e13a570bd2c4d5514465a0368862df51
gpg: DBG: ecc_verify  s_r:+8c80ec4ba8bf6c0bd09cd53d70e3a72aabf49cc7c2400fd0f4d96d51869091a6
gpg: DBG: ecc_verify  s_s:+7dc4181d3f90b49013653d4c0da607c847bdc04d9a6eddb55d9d4b378129556b
gpg: DBG: ecc_verify    => Good
gpg: DBG: finish_lookup: checking key 9A8FF7B7 (all)(req_usage=0)
gpg: DBG: 	using key 9A8FF7B7
gpg: DBG: free_packet() type=6
gpg: DBG: free_packet() type=13
gpg: DBG: free_packet() type=2
gpg: DBG: free_packet() type=14
gpg: DBG: free_packet() type=2
gpg: DBG: [no clock] keydb_search enter
gpg: DBG: keydb_search: 1 search descriptions:
gpg: DBG: keydb_search   0: NEXT
gpg: DBG: internal_keydb_search: searching keybox (resource 0 of 1)
gpg: DBG: internal_keydb_search: searched keybox (resource 0 of 1) => Succes
gpg: DBG: [no clock] keydb_search leave (found)
gpg: DBG: [no clock] keydb_get_keyblock enter
gpg: DBG: parse_packet(iob=9): type=6 length=111 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=9): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=9): type=13 length=28 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=9): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=9): type=2 length=179 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=9): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=9): type=14 length=115 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=9): type=2 length=152 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=9): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: iobuf-9.0: underflow: buffer size: 639; still buffered: 0 => space for 639 bytes
gpg: DBG: iobuf-9.0: close '?'
gpg: DBG: [no clock] keydb_get_keyblock leave
gpg: DBG: get_keygrip for public key
gpg: DBG: keygrip= 8a7d0af91a559e9766ac187d8febc747b6f0b329
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: NIST P-384
gpg: DBG: ecc_verify    p:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000ffffffff
gpg: DBG: ecc_verify    a:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000fffffffc
gpg: DBG: ecc_verify    b:+b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875a \
gpg: DBG:                  c656398d8a2ed19d2a85c8edd3ec2aef
gpg: DBG: ecc_verify  g.X:+aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a38 \
gpg: DBG:                  5502f25dbf55296c3a545e3872760ab7
gpg: DBG: ecc_verify  g.Y:+3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c0 \
gpg: DBG:                  0a60b1ce1d7e819d7a431d7c90ea0e5f
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf \
gpg: DBG:                  581a0db248b0a77aecec196accc52973
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [776 bit]
gpg: DBG:                  0408540426c71c9742ec8cbabc9c17d8f1db3193530ca88d8709a96fd0cdeb06 \
gpg: DBG:                  06117ca58589f9bd29336aab62cab315a46945480510079bde0ae256a216467a \
gpg: DBG:                  648535df857c0b7a3980b84981570939effecd6f0a5e856b692e7f7931e7f63f \
gpg: DBG:                  9c
gpg: DBG: ecc_verify data:+15d3a3eb99d010e53a74fdf9ea2538cee7bb68d8fef01571860aad03df46b8e9 \
gpg: DBG:                  1bdedc3920a3940001a4a012f4613f81
gpg: DBG: ecc_verify  s_r:+f26744b80828286635c6404bb2ff4001e7b0a7b3037b2880dde17a9d88706e39 \
gpg: DBG:                  c735e36d4ff30b517ad8ec89e16f3663
gpg: DBG: ecc_verify  s_s:+398416acc1f859873fa6e3e7aab1ad9ff2bb79c53bd52385caa74d7a9b13995a \
gpg: DBG:                  5d87cd59adfca1630db211c7480ec0a4
gpg: DBG: ecc_verify    => Good
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: NIST P-384
gpg: DBG: ecc_verify    p:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000ffffffff
gpg: DBG: ecc_verify    a:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000fffffffc
gpg: DBG: ecc_verify    b:+b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875a \
gpg: DBG:                  c656398d8a2ed19d2a85c8edd3ec2aef
gpg: DBG: ecc_verify  g.X:+aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a38 \
gpg: DBG:                  5502f25dbf55296c3a545e3872760ab7
gpg: DBG: ecc_verify  g.Y:+3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c0 \
gpg: DBG:                  0a60b1ce1d7e819d7a431d7c90ea0e5f
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf \
gpg: DBG:                  581a0db248b0a77aecec196accc52973
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [776 bit]
gpg: DBG:                  0408540426c71c9742ec8cbabc9c17d8f1db3193530ca88d8709a96fd0cdeb06 \
gpg: DBG:                  06117ca58589f9bd29336aab62cab315a46945480510079bde0ae256a216467a \
gpg: DBG:                  648535df857c0b7a3980b84981570939effecd6f0a5e856b692e7f7931e7f63f \
gpg: DBG:                  9c
gpg: DBG: ecc_verify data:+112eb5d664a64ea12cb768a08ee5dc85fb126095f08ac50d9031d5ac5a7b853a \
gpg: DBG:                  7b14d470e55ace4fbe6c24a42176cc68
gpg: DBG: ecc_verify  s_r:+29464d3fd4b3d1f6dfc706fc96437adbb936490d6ba73fe9e9edf038b8aac4c4 \
gpg: DBG:                  460d96fc8c3c9da6db2a9fed5e0372d3
gpg: DBG: ecc_verify  s_s:+329b59f8d650dd4a80e5ef86d6b8c3ecbff3927a33fd6e352f8fd091a649abd9 \
gpg: DBG:                  e8c5bc4425d5e82026587e40c553b81f
gpg: DBG: ecc_verify    => Good
gpg: DBG: finish_lookup: checking key 9669FBDC (all)(req_usage=0)
gpg: DBG: 	using key 9669FBDC
gpg: DBG: free_packet() type=6
gpg: DBG: free_packet() type=13
gpg: DBG: free_packet() type=2
gpg: DBG: free_packet() type=14
gpg: DBG: free_packet() type=2
gpg: DBG: [no clock] keydb_search enter
gpg: DBG: keydb_search: 1 search descriptions:
gpg: DBG: keydb_search   0: NEXT
gpg: DBG: internal_keydb_search: searching keybox (resource 0 of 1)
gpg: DBG: internal_keydb_search: searched keybox (resource 0 of 1) => Succes
gpg: DBG: [no clock] keydb_search leave (found)
gpg: DBG: [no clock] keydb_get_keyblock enter
gpg: DBG: parse_packet(iob=10): type=6 length=111 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=10): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=10): type=13 length=25 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=10): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=10): type=2 length=179 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=10): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=10): type=14 length=115 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=10): type=2 length=152 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=10): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: iobuf-10.0: underflow: buffer size: 636; still buffered: 0 => space for 636 bytes
gpg: DBG: iobuf-10.0: close '?'
gpg: DBG: [no clock] keydb_get_keyblock leave
gpg: DBG: get_keygrip for public key
gpg: DBG: keygrip= 49d9c325915d9f7ee14a91c6849a4f0051013a81
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: NIST P-384
gpg: DBG: ecc_verify    p:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000ffffffff
gpg: DBG: ecc_verify    a:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000fffffffc
gpg: DBG: ecc_verify    b:+b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875a \
gpg: DBG:                  c656398d8a2ed19d2a85c8edd3ec2aef
gpg: DBG: ecc_verify  g.X:+aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a38 \
gpg: DBG:                  5502f25dbf55296c3a545e3872760ab7
gpg: DBG: ecc_verify  g.Y:+3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c0 \
gpg: DBG:                  0a60b1ce1d7e819d7a431d7c90ea0e5f
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf \
gpg: DBG:                  581a0db248b0a77aecec196accc52973
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [776 bit]
gpg: DBG:                  0417cc7eb18bf16bcf4bffd22e62b6b0a4e0140887b4486aebd82dc6f96f55fa \
gpg: DBG:                  09559fd109a1f9507b3c6c26e03287b50d593c12648ba76541dbed335b9edfa7 \
gpg: DBG:                  73b14f6e6c99326bfdb405d49d2c4b655bc59361fc8c53478aef4f08526beb25 \
gpg: DBG:                  10
gpg: DBG: ecc_verify data:+10c12839110658c481c185e4d5a49ff26adac4da5c8e6c4fda3501cb892a9f6e \
gpg: DBG:                  6e1079de00e2388da3f514195dae58c8
gpg: DBG: ecc_verify  s_r:+9fbf5976859e5fe297f4ba64b5610ccf7df01958405afc449c5631a38a2c5e93 \
gpg: DBG:                  e8eadfa8780165d7e6d4390addba9ec9
gpg: DBG: ecc_verify  s_s:+a121de8d288777925a2d422a01b6a5af56c8e1aee90060fa9a0e57aa9b50c794 \
gpg: DBG:                  dd3e79f9625b6eea3670a9e494b853ad
gpg: DBG: ecc_verify    => Good
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: NIST P-384
gpg: DBG: ecc_verify    p:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000ffffffff
gpg: DBG: ecc_verify    a:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000fffffffc
gpg: DBG: ecc_verify    b:+b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875a \
gpg: DBG:                  c656398d8a2ed19d2a85c8edd3ec2aef
gpg: DBG: ecc_verify  g.X:+aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a38 \
gpg: DBG:                  5502f25dbf55296c3a545e3872760ab7
gpg: DBG: ecc_verify  g.Y:+3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c0 \
gpg: DBG:                  0a60b1ce1d7e819d7a431d7c90ea0e5f
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf \
gpg: DBG:                  581a0db248b0a77aecec196accc52973
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [776 bit]
gpg: DBG:                  0417cc7eb18bf16bcf4bffd22e62b6b0a4e0140887b4486aebd82dc6f96f55fa \
gpg: DBG:                  09559fd109a1f9507b3c6c26e03287b50d593c12648ba76541dbed335b9edfa7 \
gpg: DBG:                  73b14f6e6c99326bfdb405d49d2c4b655bc59361fc8c53478aef4f08526beb25 \
gpg: DBG:                  10
gpg: DBG: ecc_verify data:+736ffab7414bc0fffc517ea63180ba4d5e464861e9e2ad84e36c32e0718dd434 \
gpg: DBG:                  ae1dcd5c23d558dbb6dacd613a9eb416
gpg: DBG: ecc_verify  s_r:+59764984ffee0327b757d341a3cb815e9fd1a400c64f9e807dcd73d66092b206 \
gpg: DBG:                  5233907c0988b36d65e7b4e5aa795fa0
gpg: DBG: ecc_verify  s_s:+b094685caeb82f9c11ebed71df0a1009483519ced8aa333d3806300b63f26f16 \
gpg: DBG:                  841e63c088feedfc18d04b2cd2f21ba8
gpg: DBG: ecc_verify    => Good
gpg: DBG: finish_lookup: checking key D2589B33 (all)(req_usage=0)
gpg: DBG: 	using key D2589B33
gpg: DBG: [no clock] decryption start
gpg: DBG: get_keygrip for public key
gpg: DBG: keygrip= 357eeb81b9d12891fca925702ccb4b85656f5271
gpg: DBG: chan_0x000000e4 -> RESET
gpg: DBG: chan_0x000000e4 <- OK
gpg: DBG: chan_0x000000e4 -> SETKEY 357EEB81B9D12891FCA925702CCB4B85656F5271
gpg: DBG: chan_0x000000e4 <- OK
gpg: DBG: chan_0x000000e4 -> SETKEYDESC Please+enter+the+passphrase+to+unlock+the+OpenPGP+secret+key:%0A%22NP384@384+(Q)+<NP384@384>%22%0A384-bit+ECDH+key,+ID+DA3FB04B4622E15A,%0Acreated+2023-04-13+(main+key+ID+04E5836CD2589B33).%0A
gpg: DBG: chan_0x000000e4 <- OK
gpg: DBG: chan_0x000000e4 -> PKDECRYPT
gpg: DBG: chan_0x000000e4 <- S INQUIRE_MAXLEN 4096
gpg: DBG: chan_0x000000e4 <- INQUIRE CIPHERTEXT
gpg: DBG: chan_000000E4 -> [ 44 20 28 37 3a 65 6e 63 2d 76 61 6c 28 34 3a 65 ...(173 byte(s) skipped) ]
gpg: DBG: chan_0x000000e4 -> END
gpg: DBG: chan_000000E4 <- [ 44 20 28 35 3a 76 61 6c 75 65 39 37 3a 04 4c 67 ...(97 byte(s) skipped) ]
gpg: DBG: chan_0x000000e4 <- OK
gpg: DBG: DEK frame: 044c671a80febade93110b2a170b416da555adf3e93077fbbacda279ad27135f \
gpg: DBG:  2a9267f48769f399584afff8aa5a881cba5565889cff20dfe26f7dabb0f9ab7d \
gpg: DBG:  6c5ca164260c73e49b4978c7c4a8d26d5c3617b9741bc8a1000a8a19955a52f9 \
gpg: DBG:  5f
gpg: DBG: ecdh KDF params: 03010909
gpg: DBG: ecdh KDF algorithms SHA384+AES256 with aeswrap
gpg: DBG: increasing temp iobuf from 65536 to 131072
gpg: DBG: iobuf-11.0: close '?'
gpg: DBG: ecdh KDF message params are: 
052b810400221203010909416e6f6e796d6f75732053656e6465722020202045b8a7245884800eb37e72ffda3fb04b4622e15a
gpg: DBG: ECDH shared secret X is: 4c671a80febade93110b2a170b416da555adf3e93077fbbacda279ad27135f2a \
gpg: DBG:  9267f48769f399584afff8aa5a881cba
gpg: DBG: ecdh KEK is: 1f6334221af0ef32d1ba194805e00054643cd9a2b707233a4473e8099874fc69
gpg: DBG: ecdh decrypting : 9ee5ec110a649f0d00b5daaa8774b29a989e5ed90ad9e70e8ed8f629a0a39933 \
gpg: DBG:  76198ded4d36064950fd918b42d5d11e
gpg: DBG: ecdh decrypted to : 09ba422febe82ade8c0b9c3d6287b96f9a8691b8f07c97e99aebafc29d1dc0a8 \
gpg: DBG:  f312e70505050505
gpg: DBG: [no clock] decryption ready
gpg: DBG: DEK is: ba422febe82ade8c0b9c3d6287b96f9a8691b8f07c97e99aebafc29d1dc0a8f3
gpg: DBG: [no clock] keydb_new
gpg: DBG: [no clock] keydb_search enter
gpg: DBG: keydb_search: 1 search descriptions:
gpg: DBG: keydb_search   0: LONG_KID: 'DA3FB04B4622E15A'
gpg: DBG: keydb: kid_not_found_p (da3fb04b4622e15a) => indeterminate
gpg: DBG: internal_keydb_search: searching keybox (resource 0 of 1)
gpg: DBG: internal_keydb_search: searched keybox (resource 0 of 1) => Succes
gpg: DBG: [no clock] keydb_search leave (found)
gpg: DBG: [no clock] keydb_get_keyblock enter
gpg: DBG: parse_packet(iob=12): type=6 length=111 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=12): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=12): type=13 length=25 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=12): type=12 length=12 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=12): type=2 length=179 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=12): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=12): type=14 length=115 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=12): type=2 length=152 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: parse_packet(iob=12): type=12 length=6 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/keydb.c.1161)
gpg: DBG: iobuf-12.0: underflow: buffer size: 636; still buffered: 0 => space for 636 bytes
gpg: DBG: iobuf-12.0: close '?'
gpg: DBG: [no clock] keydb_get_keyblock leave
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: NIST P-384
gpg: DBG: ecc_verify    p:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000ffffffff
gpg: DBG: ecc_verify    a:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000fffffffc
gpg: DBG: ecc_verify    b:+b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875a \
gpg: DBG:                  c656398d8a2ed19d2a85c8edd3ec2aef
gpg: DBG: ecc_verify  g.X:+aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a38 \
gpg: DBG:                  5502f25dbf55296c3a545e3872760ab7
gpg: DBG: ecc_verify  g.Y:+3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c0 \
gpg: DBG:                  0a60b1ce1d7e819d7a431d7c90ea0e5f
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf \
gpg: DBG:                  581a0db248b0a77aecec196accc52973
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [776 bit]
gpg: DBG:                  0417cc7eb18bf16bcf4bffd22e62b6b0a4e0140887b4486aebd82dc6f96f55fa \
gpg: DBG:                  09559fd109a1f9507b3c6c26e03287b50d593c12648ba76541dbed335b9edfa7 \
gpg: DBG:                  73b14f6e6c99326bfdb405d49d2c4b655bc59361fc8c53478aef4f08526beb25 \
gpg: DBG:                  10
gpg: DBG: ecc_verify data:+10c12839110658c481c185e4d5a49ff26adac4da5c8e6c4fda3501cb892a9f6e \
gpg: DBG:                  6e1079de00e2388da3f514195dae58c8
gpg: DBG: ecc_verify  s_r:+9fbf5976859e5fe297f4ba64b5610ccf7df01958405afc449c5631a38a2c5e93 \
gpg: DBG:                  e8eadfa8780165d7e6d4390addba9ec9
gpg: DBG: ecc_verify  s_s:+a121de8d288777925a2d422a01b6a5af56c8e1aee90060fa9a0e57aa9b50c794 \
gpg: DBG:                  dd3e79f9625b6eea3670a9e494b853ad
gpg: DBG: ecc_verify    => Good
gpg: DBG: ecc_verify info: Weierstrass/Standard
gpg: DBG: ecc_verify name: NIST P-384
gpg: DBG: ecc_verify    p:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000ffffffff
gpg: DBG: ecc_verify    a:+fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe \
gpg: DBG:                  ffffffff0000000000000000fffffffc
gpg: DBG: ecc_verify    b:+b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875a \
gpg: DBG:                  c656398d8a2ed19d2a85c8edd3ec2aef
gpg: DBG: ecc_verify  g.X:+aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a38 \
gpg: DBG:                  5502f25dbf55296c3a545e3872760ab7
gpg: DBG: ecc_verify  g.Y:+3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c0 \
gpg: DBG:                  0a60b1ce1d7e819d7a431d7c90ea0e5f
gpg: DBG: ecc_verify  g.Z:+01
gpg: DBG: ecc_verify    n:+ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf \
gpg: DBG:                  581a0db248b0a77aecec196accc52973
gpg: DBG: ecc_verify    h:+01
gpg: DBG: ecc_verify    q: [776 bit]
gpg: DBG:                  0417cc7eb18bf16bcf4bffd22e62b6b0a4e0140887b4486aebd82dc6f96f55fa \
gpg: DBG:                  09559fd109a1f9507b3c6c26e03287b50d593c12648ba76541dbed335b9edfa7 \
gpg: DBG:                  73b14f6e6c99326bfdb405d49d2c4b655bc59361fc8c53478aef4f08526beb25 \
gpg: DBG:                  10
gpg: DBG: ecc_verify data:+736ffab7414bc0fffc517ea63180ba4d5e464861e9e2ad84e36c32e0718dd434 \
gpg: DBG:                  ae1dcd5c23d558dbb6dacd613a9eb416
gpg: DBG: ecc_verify  s_r:+59764984ffee0327b757d341a3cb815e9fd1a400c64f9e807dcd73d66092b206 \
gpg: DBG:                  5233907c0988b36d65e7b4e5aa795fa0
gpg: DBG: ecc_verify  s_s:+b094685caeb82f9c11ebed71df0a1009483519ced8aa333d3806300b63f26f16 \
gpg: DBG:                  841e63c088feedfc18d04b2cd2f21ba8
gpg: DBG: ecc_verify    => Good
gpg: DBG: finish_lookup: checking key D2589B33 (all)(req_usage=0)
gpg: DBG: 	using key D2589B33
gpg: DBG: [no clock] keydb_release
gpg: DBG: free_packet() type=6
gpg: DBG: free_packet() type=13
gpg: DBG: free_packet() type=2
gpg: DBG: free_packet() type=14
gpg: DBG: free_packet() type=2
gpg: DBG: free_packet() type=6
gpg: DBG: free_packet() type=13
gpg: DBG: free_packet() type=2
gpg: DBG: free_packet() type=14
gpg: DBG: free_packet() type=2
gpg: DBG: [no clock] keydb_release
gpg: DBG: [no clock] get_session_key leave
gpg: public key encrypted data: good DEK
gpg: AES256.OCB encrypted data
gpg: DBG: thekey: ba422febe82ade8c0b9c3d6287b96f9a8691b8f07c97e99aebafc29d1dc0a8f3
gpg: DBG: iobuf-1.2: push 'aead_decode_filter'
gpg: DBG: iobuf chain: 1.2 'aead_decode_filter' filter_eof=0 start=0 len=0
gpg: DBG: armor-filter: control: 5
gpg: DBG: iobuf chain: 1.1 'armor_filter' filter_eof=0 start=181 len=243
gpg: DBG: iobuf chain: 1.0 'file_filter(fd)' filter_eof=0 start=374 len=399
gpg: DBG: iobuf-1.2: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.2: underflow: A->FILTER (65536 bytes)
gpg: DBG: aead_underflow: size=65536 len=0
gpg: DBG: decrypt: chunklen=0 total=0 size=65536 len=30 eof
gpg: DBG: nonce: 52d91a4436bb8a77af08ee53b17e7d
gpg: DBG: authdata: d4010902100000000000000000
gpg: DBG: ndecrypted: 30 (nchunk=30)
gpg: DBG: eof seen: holdback has the last and final tag
gpg: DBG: tag: e0f16866955fb596d2400dd0fec21af9
gpg: DBG: tag is valid
gpg: DBG: nonce: 52d91a4436bb8a77af08ee53b17e7c
gpg: DBG: authdata: d4010902100000000000000001000000000000001e
gpg: DBG: tag: 2b6b1f0031daf8b3f1abc86a4f045b99
gpg: DBG: final tag is valid
gpg: DBG: aead_underflow: returning 30 (Einde van bestand)
gpg: DBG: iobuf-1.2: A->FILTER() returned rc=-1 (EOF), read 30 bytes
gpg: DBG: parse_packet(iob=1): type=8 length=0 (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/mainproc.c.1641)
# off=181 ctb=a3 tag=8 hlen=1 plen=0 indeterminate
:compressed packet: algo=2
gpg: DBG: iobuf-1.3: push 'compress_filter'
gpg: DBG: iobuf chain: 1.3 'compress_filter' filter_eof=0 start=0 len=0
gpg: DBG: iobuf chain: 1.2 '?' filter_eof=1 start=2 len=30
gpg: DBG: armor-filter: control: 5
gpg: DBG: iobuf chain: 1.1 'armor_filter' filter_eof=0 start=243 len=243
gpg: DBG: iobuf chain: 1.0 'file_filter(fd)' filter_eof=0 start=374 len=399
gpg: DBG: iobuf-1.3: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.3: underflow: A->FILTER (65536 bytes)
gpg: DBG: begin inflate: avail_in=0, avail_out=65536, inbuf=2048
gpg: DBG: iobuf-1.2: reading to external buffer, 1024 bytes
gpg: DBG: iobuf-1.2: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.2: underflow: eof (pending eof)
gpg: DBG: iobuf-1.2: filter popped (pending EOF returned)
gpg: DBG: armor-filter: control: 5
gpg: DBG: iobuf chain: 1.1 'armor_filter' filter_eof=0 start=243 len=243
gpg: DBG: iobuf chain: 1.0 'file_filter(fd)' filter_eof=0 start=374 len=399
gpg: DBG: enter inflate: avail_in=28, avail_out=65536
gpg: DBG: leave inflate: avail_in=0, avail_out=65516, zrc=1
gpg: DBG: do_uncompress: returning 20 bytes (0 ignored)
gpg: DBG: iobuf-1.3: A->FILTER() returned rc=-1 (EOF), read 20 bytes
gpg: DBG: parse_packet(iob=1): type=11 length=18 (new_ctb) (parse./home/wk/b/gnupg/dist/PLAY-release/gnupg-w32-2.4.0/g10/mainproc.c.1641)
# off=183 ctb=cb tag=11 hlen=2 plen=18 new-ctb
:literal data packet:
	mode b (62), created 1681393425, name="",
	raw data: 12 bytes
gpg: original file name=''
Without-PW
gpg: DBG: free_packet() type=11
gpg: DBG: iobuf-1.3: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.3: underflow: eof (pending eof)
gpg: DBG: iobuf-1.3: filter popped (pending EOF returned)
gpg: DBG: armor-filter: control: 5
gpg: DBG: iobuf chain: 1.1 'armor_filter' filter_eof=0 start=243 len=243
gpg: DBG: iobuf chain: 1.0 'file_filter(fd)' filter_eof=0 start=374 len=399
gpg: DBG: free_packet() type=63
gpg: DBG: free_packet() type=8
gpg: DBG: iobuf-1.1: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.1: underflow: A->FILTER (65536 bytes)
gpg: DBG: armor-filter: control: 3
gpg: DBG: iobuf-1.0: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.0: underflow: A->FILTER (65536 bytes)
gpg: DBG: iobuf-1.0: A->FILTER() returned rc=0 (ok), read 0 bytes
gpg: DBG: iobuf-1.0: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.0: underflow: A->FILTER (65536 bytes)
gpg: DBG: iobuf-1.0: A->FILTER() returned rc=0 (ok), read 0 bytes
gpg: DBG: iobuf-1.0: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.0: underflow: A->FILTER (65536 bytes)
gpg: DBG: iobuf-1.0: A->FILTER() returned rc=0 (ok), read 0 bytes
gpg: DBG: iobuf-1.1: A->FILTER() returned rc=-1 (EOF), read 0 bytes
gpg: DBG: armor-filter: control: 2
gpg: DBG: iobuf-1.1: pop in underflow (nothing buffered, got EOF)
gpg: DBG: iobuf chain: 1.0 'file_filter(fd)' filter_eof=0 start=0 len=0
gpg: decryption okay
gpg: DBG: free_packet() type=20
gpg: DBG: iobuf-1.0: underflow: buffer size: 65536; still buffered: 0 => space for 65536 bytes
gpg: DBG: iobuf-1.0: underflow: A->FILTER (65536 bytes)
gpg: DBG: iobuf-1.0: A->FILTER() returned rc=0 (ok), read 0 bytes
gpg: DBG: iobuf-1.0: close 'file_filter(fd)'
gpg: DBG: [no clock] stop
gpg: keydb: handles=3 locks=0 parse=10 get=10
gpg:        build=0 update=0 insert=0 delete=0
gpg:        reset=0 found=10 not=0 cache=0 not=0
gpg: kid_not_found_cache: count=0 peak=0 flushes=0
gpg: sig_cache: total=19 cached=0 good=0 bad=0
gpg: objcache: keys=15/15/0 chains=368,1..1 buckets=383/20 attic=241
gpg: objcache: uids=8/8/0 chains=99,1..1 buckets=107/20
gpg: random usage: poolsize=600 mixed=0 polls=0/0 added=0/0
              outmix=0 getlvl1=0/0 getlvl2=0/0
gpg: rndjent stat: collector=0x00000000 calls=0 bytes=0
gpg: secmem usage: 0/32768 bytes in 0 blocks
*/
