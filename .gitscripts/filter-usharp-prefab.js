const fs = require("fs");

const guidPattern = '\\{fileID: .*(\n     *type: [0-9]+)?\\}';
const pattern = new RegExp(
  [
    `    - target: ${guidPattern}\n`,
    "      propertyPath: (serializedProgramAsset|serializationData\\..*)\n",
    "      value:.*\n",
    `      objectReference: ${guidPattern}\n`,
    "|",
    `  serializedProgramAsset: ${guidPattern}\n`,
    "|",
    "    SerializedFormat: [02]\n",
  ].join(""),
  "mg"
);

const input = process.argv[2]
  ? fs.createReadStream(process.argv[2])
  : process.stdin;

const chunks = [];
input.on("data", (chunk) => chunks.push(chunk));
input.on("end", () => {
  process.stdout.write(Buffer.concat(chunks).toString().replace(pattern, ""));
});
