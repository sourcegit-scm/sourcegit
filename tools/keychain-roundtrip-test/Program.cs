using SourceGit.Native;

const string svc = "SourceGit-kctest";
var acct = Guid.NewGuid().ToString("N");
var secret = "sk-test-é-\"quote\"-\\back\\ -value-12345";

Console.WriteLine($"account={acct}");
Console.WriteLine($"read(before) = {Show(MacOSKeychain.Read(svc, acct))}");

var w1 = MacOSKeychain.Write(svc, acct, secret);
Console.WriteLine($"write add -> {w1}");
var r1 = MacOSKeychain.Read(svc, acct);
Console.WriteLine($"read(after add) = {Show(r1)}  match={r1 == secret}");

var secret2 = "updated-ñ-value";
var w2 = MacOSKeychain.Write(svc, acct, secret2);
Console.WriteLine($"write update -> {w2}");
var r2 = MacOSKeychain.Read(svc, acct);
Console.WriteLine($"read(after update) = {Show(r2)}  match={r2 == secret2}");

MacOSKeychain.Delete(svc, acct);
var r3 = MacOSKeychain.Read(svc, acct);
Console.WriteLine($"read(after delete) = {Show(r3)}  deleted={r3 == null}");

bool ok = r1 == secret && r2 == secret2 && r3 == null;
Console.WriteLine(ok ? "\nKEYCHAIN ROUND-TRIP OK" : "\nKEYCHAIN ROUND-TRIP FAILED");
return ok ? 0 : 1;

static string Show(string s) => s == null ? "<null>" : "«" + s + "»";
