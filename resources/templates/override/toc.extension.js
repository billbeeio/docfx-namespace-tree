/**
 * This method will be called at the start of exports.transform in toc.html.js
 */
exports.preTransform = function (model)
{
  if (model._key != null && model._key == "intermediate_files/metadata/resources/toc.yml")
  {
    console.log("############ nesting namespace toc script start ############");

//    console.log("--------------- input tree start --------------------");
//    printTreeInfo(model, 0, true);
//    console.log("--------------- input tree end --------------------");

    var resultTree = {};
    var length = model.items.length;
    for (var i = 0; i < length; i++) 
    {
      resultTree = nestNamespaces(model.items[i], resultTree);
    }

    //console.log("--------------- result tree start --------------------");
    //printTreeInfo(resultTree, 0, false);
    //console.log("--------------- result tree end --------------------");

    console.log("--------------- transform result tree start --------------------");
    var transformedResultTree = transformTree(resultTree, 0);
    model.items = transformedResultTree.items;
//    printTreeInfo(model, 0, true);
    console.log("--------------- transform result tree end --------------------");

    console.log("############ nesting namespace toc script end ############");
  }  
  return model;
}

/*****************************************************************************/
/**
 * This method will be called at the end of exports.transform in toc.html.js
 */
exports.postTransform = function (model)
{
  return model;
}

/*****************************************************************************/
function nestNamespaces(node, resultTree)
{   
  // possibly class on root level
  if (node.items == undefined)
  {
    return nestClass(node, resultTree);
  }

  var current = resultTree;
  var fullNameSpace = node.name;
  var splitNameSpace = fullNameSpace.split('.');
  
  //console.log("namespace:" + fullNameSpace.toString());
  
  var referenceName = "";

  for(var i = 0; i < splitNameSpace.length; i++)
  {
    var partialNameSpace = splitNameSpace[i];
    var partialNameSpaceId = '_' + partialNameSpace;

    if(current[partialNameSpaceId] == undefined)
    {
      current[partialNameSpaceId] = {};
      //console.log("adding new tree node: " + partialNameSpaceId.toString());
    }

    current = current[partialNameSpaceId];  
    current.name = partialNameSpace;    

    if (i < splitNameSpace.length - 1)
    {
      if (referenceName != "")
        referenceName += ".";
      referenceName += partialNameSpace;

      current.href = referenceName + ".html";
      current.topicHref = referenceName + ".html";
      current.topicUid = referenceName;
    }
    else // takeover original values for most nested namespace
    {
      current.href = node.href;
      current.topicHref = node.topicHref;
      current.topicUid = node.topicUid;      
    }
  }
  
  // add leaf list (classes)
  current.items = node.items;
  return resultTree;
}

/*****************************************************************************/
function nestClass(node, resultTree)
{
  var current = resultTree;
  var classId = '_' + node.name;
  if(current[classId] == undefined)
  {
    current[classId] = {};
    //console.log("adding new class tree node: " + classId.toString());
  }

  current = current[classId];  
  current.name = node.name;    

  current.href = node.href;
  current.topicHref = node.topicHref;
  current.topicUid = node.topicUid;      

  return resultTree;
}

/*****************************************************************************/
function transformTree(tree, level)
{
  for(var propName in tree)
  {    
    if (propName.toString().charAt(0) != '_')
      continue;
      
    var propValue = tree[propName];

    if (tree.items == null)
    {
      tree.items = [propValue];
    }
    else
    {
      tree.items[tree.items.length] = propValue;
    }

    transformTree(propValue, level + 1);

    delete tree[propName];
  }
  
  return tree;
}

/*****************************************************************************/
function printTreeInfo(tree, level, detailed)
{
  var levelIndent = "";
  for (var i = 0; i <= level; i++)
    levelIndent = levelIndent + "  ";
  
  for(var propName in tree)
  {
    if (!detailed)
    {
      if (propName == "href" || propName == "topicHref" || propName == "topicUid")
        continue;
    }
      propValue = tree[propName];
      console.log(levelIndent.toString() + propName.toString() + ":" + propValue.toString());

    printTreeInfo(propValue, level + 1, detailed);
  }
}

/*****************************************************************************/
/** print name and value of all the item's properties */
function printPropertyNames(item)
{
  if (item == null)
    return;

  for(var propName in item)
  {
      propValue = item[propName];
      console.log("name: " + propName.toString() + "; value: " + propValue.toString());
  }
}
