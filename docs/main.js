import { dotnet } from './dotnet.js'

// Get exported methods from the .NET assembly
const { getAssemblyExports, getConfig } = await dotnet
	.withDiagnosticTracing(false)
	.create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

// Access JSExport methods using exports.<Namespace>.<Type>.<Method>
function processM() {
	let microtext = document.getElementById("microcode");

	const result = exports.TD_CMAKit.Program.MicroCodeImg(microtext.value);
	const result2 = exports.TD_CMAKit.Program.MicroCode(microtext.value);

	let blob = new Blob([result], { 'type': 'image/png' });
	let url = URL.createObjectURL(blob);
	let img = document.getElementById("img");
	img.src = url;

	let mc = JSON.parse(result2);
	
	const bmc = document.getElementById("bmc");
	bmc.innerText = mc.bmc.join("\n");

	const ah = document.getElementById("ah");
	ah.innerText = mc.ah.join("\n");

	const pmc = document.getElementById("pmc");
	pmc.innerText = mc.pmc.join("\n");
}

// Access JSExport methods using exports.<Namespace>.<Type>.<Method>
function processA() {
	let microtext = document.getElementById("asmcode");

	const result = exports.TD_CMAKit.Program.AsmCode(microtext.value);

	const basm = document.getElementById("basm");
	basm.innerText = result.join("\n");
}

const btnM = document.getElementById("btnM");
btnM.onclick = processM;
const btnA = document.getElementById("btnA");
btnA.onclick = processA;
