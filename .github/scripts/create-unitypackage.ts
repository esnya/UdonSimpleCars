import { archive } from "@natsuneko-laboratory/unitypackage";
import fs from "fs";
import glob from "glob";
import mkdirp from "mkdirp";
import path from "path";
import os from "os";
import rimraf from "rimraf";
import recursiveCopy from "recursive-copy";

async function createUnityPackage(workspace: string)
{
  const pkg = JSON.parse(
    (await fs.promises.readFile(`${workspace}/package.json`)).toString()
  );

  const tmp = await fs.promises.mkdtemp(path.join(os.tmpdir(), 'pkg'));
  try {
    const name: string = pkg.name;
    const displayName: string = pkg.displayName;
    const safeDisplayName = displayName.replace(/[\s\\\/]/g, "-");
    const unitypackageName = `dist/${safeDisplayName}-v${process.env["VERSION"]}.unitypackage`;
    console.log(`${name}: ${displayName} > ${unitypackageName}`);

    const rootDir = path.join(
      tmp,
      "Assets",
      displayName.replace(/[\s\\\/]/g, "-")
    );
    await mkdirp(rootDir);
    await recursiveCopy(workspace, rootDir);
    await fs.promises.copyFile(`${workspace}.meta`, `${rootDir}.meta`);

    const metaFiles = glob.sync(`${tmp}/**/*.meta`.replace(/\\/g, "/"));
    await archive(
      metaFiles.map((p) => path.relative(tmp, p)),
      tmp,
      unitypackageName
    );
  } finally {
    await new Promise<void>((resolve, reject) =>
      rimraf(tmp, (e) => (e ? reject(e) : resolve()))
    );
  }
}

(async () => {
  await mkdirp("dist");

  const pkg = JSON.parse(
    (await fs.promises.readFile("package.json")).toString()
  );

  const workspaces: string[] | undefined = pkg.workspaces;
  if (workspaces) await workspaces.reduce((p, workspace) => p.then(() => createUnityPackage(workspace)), Promise.resolve());
  else await createUnityPackage(process.cwd());
})().catch(console.error);
