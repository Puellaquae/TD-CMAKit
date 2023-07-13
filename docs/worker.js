import { dotnet } from './dotnet.js'

addEventListener("message", async e => {
    // Get exported methods from the .NET assembly
    const { getAssemblyExports, getConfig } = await dotnet
        .withDiagnosticTracing(false)
        .create();

    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);

    const processA = exports.TD_CMAKit.Program.AsmCode;
    const processB = exports.TD_CMAKit.Program.MicroCode;
    const processC = exports.TD_CMAKit.Program.MicroCodeImg;

    console.log(`call ${e.data.fn}`);
    if (e.data.fn === "a") {
        try {
            const res = processA(e.data.data);
            console.log(`process a ok`);
            postMessage({
                stats: "ok",
                fn: e.data.fn,
                data: res
            })
        } catch (err) {
            postMessage({
                stats: "err",
                fn: e.data.fn,
                err
            })
        }
    } else if (e.data.fn === "b") {
        try {
            const res = processB(e.data.data);
            console.log(`process b ok`);
            postMessage({
                stats: "ok",
                fn: e.data.fn,
                data: JSON.parse(res)
            })
        } catch (err) {
            postMessage({
                stats: "err",
                fn: e.data.fn,
                err
            })
        }
    } else if (e.data.fn === "c") {
        try {
            const res = processC(e.data.data);
            console.log(`process c ok`);
            postMessage({
                stats: "ok",
                fn: e.data.fn,
                data: res
            });
        } catch (err) {
            postMessage({
                stats: "err",
                fn: e.data.fn,
                err
            })
        }
    } else {
        postMessage({
            stats: "err",
            fn: e.data.fn,
            err: "unknown fn call"
        })
    }
})