let worker = new Worker("worker.js", { type: "module" });

function processM() {
	const microtext = document.getElementById("microcode");
	const bmc = document.getElementById("bmc");
	const ah = document.getElementById("ah");
	const pmc = document.getElementById("pmc");
	const img = document.getElementById("img");
	const imginfo = document.getElementById("imginfo");

	worker.onmessage = e => {
		if (e.data.fn === "b") {
			if (e.data.stats === "ok") {
				bmc.innerText = e.data.data.bmc.join("\n");
				ah.innerText = e.data.data.ah.join("\n");
				pmc.innerText = e.data.data.pmc.join("\n");
			} else {
				pmc.innerText = e.data.err;
			}
			worker.postMessage({
				fn: "c",
				data: microtext.value
			});
		} else if (e.data.fn === "c") {
			if (e.data.stats === "ok") {
				let blob = new Blob([e.data.data], { 'type': 'image/png' });
				let url = URL.createObjectURL(blob);
				imginfo.hidden = true;
				img.src = url;
			} else {
				imginfo.innerText = e.data.err;
			}
		}
	}
	pmc.innerText = "Processing...";
	ah.innerText = "";
	bmc.innerText = "";
	img.src = "";
	imginfo.hidden = false;
	imginfo.innerText = "Processing...";
	worker.postMessage({
		fn: "b",
		data: microtext.value
	});

}

// Access JSExport methods using exports.<Namespace>.<Type>.<Method>
function processA() {
	const microtext = document.getElementById("asmcode");
	const basm = document.getElementById("basm");

	worker.onmessage = e => {
		if (e.data.fn === "a") {
			if (e.data.stats === "ok") {
				basm.innerText = e.data.data.join("\n");
			} else {
				basm.innerText = e.data.err;
			}
		}
	}

	worker.postMessage({
		fn: "a",
		data: microtext.value
	});

	basm.innerText = "Processing...";
}

const btnM = document.getElementById("btnM");
btnM.onclick = processM;
const btnA = document.getElementById("btnA");
btnA.onclick = processA;
