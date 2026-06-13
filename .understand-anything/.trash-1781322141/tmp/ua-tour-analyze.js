#!/usr/bin/env node
'use strict';
const fs = require('fs');

function main() {
  const nodesPath = process.argv[2];
  const layersPath = process.argv[3];
  const edgesPath = process.argv[4];
  const outPath = process.argv[5];
  if (!nodesPath || !layersPath || !edgesPath || !outPath) {
    console.error('Usage: node ua-tour-analyze.js <nodes> <layers> <edges> <out>');
    process.exit(1);
  }

  const nodes = JSON.parse(fs.readFileSync(nodesPath, 'utf8'));
  const layers = JSON.parse(fs.readFileSync(layersPath, 'utf8'));
  const edges = JSON.parse(fs.readFileSync(edgesPath, 'utf8'));

  const nodeById = new Map();
  for (const n of nodes) nodeById.set(n.id, n);
  const validIds = new Set(nodeById.keys());

  // Only consider edges between known nodes
  const goodEdges = edges.filter(e => validIds.has(e.source) && validIds.has(e.target));

  // Fan-in / fan-out
  const fanIn = new Map();
  const fanOut = new Map();
  for (const id of validIds) { fanIn.set(id, 0); fanOut.set(id, 0); }
  for (const e of goodEdges) {
    fanOut.set(e.source, fanOut.get(e.source) + 1);
    fanIn.set(e.target, fanIn.get(e.target) + 1);
  }

  const byFanIn = [...validIds].map(id => ({ id, fanIn: fanIn.get(id), name: nodeById.get(id).name }))
    .sort((a, b) => b.fanIn - a.fanIn).slice(0, 20);
  const byFanOut = [...validIds].map(id => ({ id, fanOut: fanOut.get(id), name: nodeById.get(id).name }))
    .sort((a, b) => b.fanOut - a.fanOut).slice(0, 20);

  // Entry point scoring
  const fanOutVals = [...validIds].map(id => fanOut.get(id)).sort((a, b) => a - b);
  const fanInVals = [...validIds].map(id => fanIn.get(id)).sort((a, b) => a - b);
  const p90FanOut = fanOutVals[Math.floor(fanOutVals.length * 0.9)] || 0;
  const p25FanIn = fanInVals[Math.floor(fanInVals.length * 0.25)] || 0;

  const codeEntryNames = new Set([
    'index.ts','index.js','main.ts','main.js','app.ts','app.js','server.ts','server.js',
    'mod.rs','main.go','main.py','main.rs','manage.py','app.py','wsgi.py','asgi.py','run.py',
    '__main__.py','Application.java','Main.java','Program.cs','config.ru','index.php',
    'App.swift','Application.kt','main.cpp','main.c','App.cs'
  ]);

  const epScores = [];
  for (const n of nodes) {
    let score = 0;
    const fp = (n.filePath || '').replace(/\\/g, '/');
    const depth = fp.split('/').length;
    if (n.type === 'document') {
      if (/(^|\/)README\.md$/i.test(fp) && depth <= 1) score += 5;
      else if (/\.md$/i.test(fp) && depth <= 1) score += 2;
    } else {
      if (codeEntryNames.has(n.name)) score += 3;
      if (depth <= 2) score += 1;
      if (fanOut.get(n.id) >= p90FanOut && p90FanOut > 0) score += 1;
      if (fanIn.get(n.id) <= p25FanIn) score += 1;
    }
    if (score > 0) epScores.push({ id: n.id, score, name: n.name, summary: n.summary });
  }
  // App.cs special: it's the documented conceptual entry point
  epScores.sort((a, b) => b.score - a.score);
  const entryPointCandidates = epScores.slice(0, 8);

  // BFS from top code entry point following imports/calls/depends_on forward edges
  const forwardTypes = new Set(['imports', 'calls', 'depends_on', 'inherits']);
  const adj = new Map();
  for (const id of validIds) adj.set(id, []);
  for (const e of goodEdges) {
    if (forwardTypes.has(e.type) && (e.direction === undefined || e.direction === 'forward')) {
      adj.get(e.source).push(e.target);
    }
  }

  // pick start: prefer App.cs, else top non-document candidate
  let startNode = null;
  const appNode = nodes.find(n => /\/App\.cs$/.test((n.filePath || '').replace(/\\/g, '/')));
  if (appNode) startNode = appNode.id;
  if (!startNode) {
    const codeEp = entryPointCandidates.find(c => !c.id.startsWith('document:'));
    startNode = codeEp ? codeEp.id : (nodes[0] && nodes[0].id);
  }

  const order = [];
  const depthMap = {};
  const visited = new Set();
  if (startNode) {
    const q = [[startNode, 0]];
    visited.add(startNode);
    while (q.length) {
      const [id, d] = q.shift();
      order.push(id);
      depthMap[id] = d;
      for (const nb of (adj.get(id) || [])) {
        if (!visited.has(nb)) { visited.add(nb); q.push([nb, d + 1]); }
      }
    }
  }
  const byDepth = {};
  for (const id of order) {
    const d = depthMap[id];
    (byDepth[d] = byDepth[d] || []).push(id);
  }

  // Non-code inventory
  const nonCodeFiles = { documentation: [], infrastructure: [], data: [], config: [] };
  for (const n of nodes) {
    const t = n.type;
    const rec = { id: n.id, name: n.name, type: t, summary: n.summary };
    if (t === 'document') nonCodeFiles.documentation.push(rec);
    else if (t === 'service' || t === 'pipeline' || t === 'resource') nonCodeFiles.infrastructure.push(rec);
    else if (t === 'table' || t === 'schema' || t === 'endpoint') nonCodeFiles.data.push(rec);
    else if (t === 'config') nonCodeFiles.config.push(rec);
  }

  // Tightly coupled clusters: bidirectional pairs then expand
  const edgeSet = new Set();
  for (const e of goodEdges) {
    if (forwardTypes.has(e.type)) edgeSet.add(e.source + '||' + e.target);
  }
  const pairs = [];
  const seenPair = new Set();
  for (const e of goodEdges) {
    if (!forwardTypes.has(e.type)) continue;
    const rev = e.target + '||' + e.source;
    const key = [e.source, e.target].sort().join('||');
    if (edgeSet.has(rev) && !seenPair.has(key)) {
      seenPair.add(key);
      pairs.push([e.source, e.target]);
    }
  }
  // undirected adjacency for expansion
  const undAdj = new Map();
  for (const id of validIds) undAdj.set(id, new Set());
  for (const e of goodEdges) {
    if (!forwardTypes.has(e.type)) continue;
    undAdj.get(e.source).add(e.target);
    undAdj.get(e.target).add(e.source);
  }
  const clusters = [];
  for (const [a, b] of pairs) {
    const cluster = new Set([a, b]);
    let changed = true;
    while (changed && cluster.size < 5) {
      changed = false;
      const candidates = new Map();
      for (const m of cluster) {
        for (const nb of undAdj.get(m)) {
          if (cluster.has(nb)) continue;
          candidates.set(nb, (candidates.get(nb) || 0) + 1);
        }
      }
      for (const [c, cnt] of candidates) {
        if (cnt >= 2 && cluster.size < 5) { cluster.add(c); changed = true; }
      }
    }
    let edgeCount = 0;
    const arr = [...cluster];
    for (const x of arr) for (const y of arr) if (x !== y && edgeSet.has(x + '||' + y)) edgeCount++;
    clusters.push({ nodes: arr, edgeCount });
  }
  clusters.sort((a, b) => b.edgeCount - a.edgeCount);
  // dedupe by node-set
  const uniqClusters = [];
  const seenSet = new Set();
  for (const c of clusters) {
    const k = [...c.nodes].sort().join('|');
    if (seenSet.has(k)) continue;
    seenSet.add(k);
    uniqClusters.push(c);
  }

  const nodeSummaryIndex = {};
  for (const n of nodes) nodeSummaryIndex[n.id] = { name: n.name, type: n.type, summary: n.summary };

  const out = {
    scriptCompleted: true,
    entryPointCandidates,
    fanInRanking: byFanIn,
    fanOutRanking: byFanOut,
    bfsTraversal: { startNode, order, depthMap, byDepth },
    nonCodeFiles,
    clusters: uniqClusters.slice(0, 10),
    layers: { count: layers.length, list: layers },
    nodeSummaryIndex,
    totalNodes: nodes.length,
    totalEdges: edges.length
  };
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));
  console.error('OK nodes=' + nodes.length + ' edges=' + edges.length + ' start=' + startNode);
}

try { main(); } catch (e) { console.error('FATAL', e && e.stack || e); process.exit(1); }
