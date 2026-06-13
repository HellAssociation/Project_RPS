#!/usr/bin/env node
"use strict";
const fs = require("fs");

function main() {
  const inPath = process.argv[2];
  const outPath = process.argv[3];
  if (!inPath || !outPath) { console.error("usage: script <in> <out>"); process.exit(1); }
  const data = JSON.parse(fs.readFileSync(inPath, "utf8"));
  const fileNodes = data.fileNodes || [];
  const allEdges = data.allEdges || [];
  const importEdges = data.importEdges || [];

  const byId = {};
  fileNodes.forEach(n => { byId[n.id] = n; });
  const ids = fileNodes.map(n => n.filePath);

  // common prefix on directory segments
  const splitDirs = p => p.split("/").slice(0, -1);
  let prefix = null;
  fileNodes.forEach(n => {
    const d = splitDirs(n.filePath);
    if (prefix === null) prefix = d.slice();
    else {
      let i = 0; while (i < prefix.length && i < d.length && prefix[i] === d[i]) i++;
      prefix = prefix.slice(0, i);
    }
  });
  // don't over-collapse if it removes all grouping; use prefix as-is
  const prefixLen = prefix.length;

  // directory groups: first segment after prefix
  const dirOf = {};
  const directoryGroups = {};
  fileNodes.forEach(n => {
    const segs = n.filePath.split("/");
    const dirsegs = segs.slice(0, -1);
    let g;
    if (dirsegs.length <= prefixLen) g = "(root)";
    else g = dirsegs[prefixLen];
    dirOf[n.id] = g;
    (directoryGroups[g] = directoryGroups[g] || []).push(n.id);
  });

  // node type groups
  const nodeTypeGroups = {};
  fileNodes.forEach(n => { (nodeTypeGroups[n.type] = nodeTypeGroups[n.type] || []).push(n.id); });

  // cross-category edges (between node-type groups)
  const ccMap = {};
  allEdges.forEach(e => {
    const s = byId[e.source], t = byId[e.target];
    if (!s || !t) return;
    if (s.type === t.type) return;
    const k = s.type + "|" + t.type + "|" + e.type;
    ccMap[k] = (ccMap[k] || 0) + 1;
  });
  const crossCategoryEdges = Object.entries(ccMap).map(([k, c]) => {
    const [fromType, toType, edgeType] = k.split("|");
    return { fromType, toType, edgeType, count: c };
  });

  // edges to use for direction: imports OR inherits/depends_on/calls/configures
  const relEdges = allEdges.filter(e =>
    ["imports", "inherits", "depends_on", "calls", "configures", "uses"].includes(e.type) &&
    byId[e.source] && byId[e.target]);

  // inter-group import freq
  const igMap = {};
  relEdges.forEach(e => {
    const a = dirOf[e.source], b = dirOf[e.target];
    if (a === b) return;
    const k = a + "|" + b;
    igMap[k] = (igMap[k] || 0) + 1;
  });
  const interGroupImports = Object.entries(igMap).map(([k, c]) => {
    const [from, to] = k.split("|"); return { from, to, count: c };
  }).sort((x, y) => y.count - x.count);

  // intra group density
  const intraGroupDensity = {};
  Object.keys(directoryGroups).forEach(g => { intraGroupDensity[g] = { internalEdges: 0, totalEdges: 0, density: 0 }; });
  relEdges.forEach(e => {
    const a = dirOf[e.source], b = dirOf[e.target];
    if (a === b) { intraGroupDensity[a].internalEdges++; intraGroupDensity[a].totalEdges++; }
    else { intraGroupDensity[a].totalEdges++; intraGroupDensity[b].totalEdges++; }
  });
  Object.keys(intraGroupDensity).forEach(g => {
    const d = intraGroupDensity[g];
    d.density = d.totalEdges ? +(d.internalEdges / d.totalEdges).toFixed(2) : 0;
  });

  // fan in / out
  const fileFanIn = {}, fileFanOut = {};
  fileNodes.forEach(n => { fileFanIn[n.id] = 0; fileFanOut[n.id] = 0; });
  relEdges.forEach(e => { fileFanOut[e.source]++; fileFanIn[e.target]++; });

  // pattern matching
  const dirPatterns = [
    [/^(routes|api|controllers|endpoints|handlers|controller|routers|blueprints|serializers)$/i, "api"],
    [/^(services|core|lib|domain|logic|composables|signals|mailers|jobs|channels)$/i, "service"],
    [/^(models|db|data|persistence|repository|entities|entity|migrations|sql|database)$/i, "data"],
    [/^(components|views|pages|ui|layouts|screens)$/i, "ui"],
    [/^(middleware|plugins|interceptors|guards)$/i, "middleware"],
    [/^(utils|helpers|common|shared|tools|util|pkg|templatetags)$/i, "utility"],
    [/^(config|constants|env|settings|management|commands)$/i, "config"],
    [/^(__tests__|test|tests|spec|specs)$/i, "test"],
    [/^(types|interfaces|schemas|contracts|dtos|dto|request|response)$/i, "types"],
    [/^hooks$/i, "hooks"],
    [/^(store|state|reducers|actions|slices)$/i, "state"],
    [/^(assets|static|public)$/i, "assets"],
    [/^(cmd|bin|internal)$/i, "entry"],
    [/^(docs|documentation|wiki)$/i, "documentation"],
    [/^(deploy|deployment|infra|infrastructure|k8s|kubernetes|helm|charts|terraform|tf|docker)$/i, "infrastructure"],
    [/^(\.github|\.gitlab|\.circleci)$/i, "ci-cd"],
  ];
  const patternMatches = {};
  Object.keys(directoryGroups).forEach(g => {
    for (const [re, label] of dirPatterns) { if (re.test(g)) { patternMatches[g] = label; break; } }
  });

  // deployment topology
  const lc = s => s.toLowerCase();
  const has = re => fileNodes.some(n => re.test(n.filePath));
  const deploymentTopology = {
    hasDockerfile: has(/(^|\/)Dockerfile/i),
    hasCompose: has(/docker-compose/i),
    hasK8s: has(/(k8s|kubernetes|helm)/i),
    hasTerraform: has(/\.tf$/i),
    hasCI: has(/(\.github\/workflows|\.gitlab-ci|Jenkinsfile)/i),
    infraFiles: fileNodes.filter(n => /(Dockerfile|docker-compose|\.tf$|\.github\/workflows|Jenkinsfile|Makefile)/i.test(n.filePath)).map(n => n.filePath)
  };

  // data pipeline
  const dataPipeline = {
    schemaFiles: fileNodes.filter(n => /\.(sql|graphql|gql|proto|prisma)$/i.test(n.filePath)).map(n => n.filePath),
    migrationFiles: fileNodes.filter(n => /migrations?\//i.test(n.filePath)).map(n => n.filePath),
    dataModelFiles: fileNodes.filter(n => /(GameData|ModeData|Data\.cs$)/.test(n.filePath) || (n.tags || []).includes("data-model")).map(n => n.filePath),
    apiHandlerFiles: fileNodes.filter(n => (n.tags || []).includes("api-handler")).map(n => n.filePath)
  };

  // doc coverage
  const groupHasDoc = {};
  Object.keys(directoryGroups).forEach(g => { groupHasDoc[g] = false; });
  const docGroups = new Set();
  fileNodes.forEach(n => { if (n.type === "document" || /\.(md|rst)$/i.test(n.filePath)) docGroups.add(dirOf[n.id]); });
  const totalGroups = Object.keys(directoryGroups).length;
  const groupsWithDocs = docGroups.size;
  const docCoverage = {
    groupsWithDocs, totalGroups,
    coverageRatio: totalGroups ? +(groupsWithDocs / totalGroups).toFixed(2) : 0,
    undocumentedGroups: Object.keys(directoryGroups).filter(g => !docGroups.has(g))
  };

  // dependency direction
  const pairNet = {};
  interGroupImports.forEach(({ from, to, count }) => {
    const key = [from, to].sort().join("|");
    pairNet[key] = pairNet[key] || {};
    pairNet[key][from + ">" + to] = count;
  });
  const dependencyDirection = [];
  Object.entries(pairNet).forEach(([key, m]) => {
    const [a, b] = key.split("|");
    const ab = m[a + ">" + b] || 0, ba = m[b + ">" + a] || 0;
    if (ab >= ba && ab > 0) dependencyDirection.push({ dependent: a, dependsOn: b });
    else if (ba > 0) dependencyDirection.push({ dependent: b, dependsOn: a });
  });

  const filesPerGroup = {}; Object.entries(directoryGroups).forEach(([g, a]) => filesPerGroup[g] = a.length);
  const nodeTypeCounts = {}; Object.entries(nodeTypeGroups).forEach(([t, a]) => nodeTypeCounts[t] = a.length);

  const result = {
    scriptCompleted: true,
    commonPrefix: prefix.join("/"),
    directoryGroups, nodeTypeGroups, crossCategoryEdges,
    interGroupImports, intraGroupDensity, patternMatches,
    deploymentTopology, dataPipeline, docCoverage, dependencyDirection,
    fileStats: { totalFileNodes: fileNodes.length, filesPerGroup, nodeTypeCounts },
    fileFanIn, fileFanOut
  };
  fs.writeFileSync(outPath, JSON.stringify(result, null, 2));
  process.exit(0);
}
try { main(); } catch (e) { console.error(e.stack || String(e)); process.exit(1); }
