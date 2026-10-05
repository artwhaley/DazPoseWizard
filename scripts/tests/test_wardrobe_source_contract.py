"""Early source checks and mechanical recipe defaults, independent of user assets."""
import importlib.util
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('recipe',Path(__file__).parents[1]/'wardrobe-recipe.py')
recipe=importlib.util.module_from_spec(spec);spec.loader.exec_module(recipe)

def elem(key,props=(),elems=()): return SimpleNamespace(id=key,props=props,elems=elems)

class SourceContract(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name)
        self.config={'fbx':'source.fbx','shoes':{'node':'pumps','articulation':'rigid-pair'}}
        self.points=[0,0,0,1,0,0,0,1,0]

    def inspect(self):
        def mesh(name,points):
            return elem(b'Geometry',(1,name.encode()+b'\x00\x01Geometry',b'Mesh'),
                        [elem(b'Vertices',(points,)),elem(b'PolygonVertexIndex',([0,1,-3] if points else [],))])
        tree=elem(b'Root',elems=[elem(b'Objects',elems=[mesh('Genesis8Female',self.points),mesh('pumps',[0,0,0,1,0,0,0,1,0])])])
        with patch.object(recipe.importlib,'import_module',return_value=SimpleNamespace(parse=lambda p:(tree,7400))):
            recipe.inspect_source(self.config,self.root,self.root)

    def test_footwear_gets_default_calibration(self):
        self.assertEqual(recipe.calibration_path(self.config),recipe.ROOT/'configs/wardrobe/paired-tiptoe-calibration.json')

    def test_barefoot_does_not_get_pose_calibration(self):
        self.assertIsNone(recipe.calibration_path({'shoes':None}))

    def test_actual_body_geometry_passes(self): self.inspect()

    def test_named_empty_body_is_rejected(self):
        self.points=[]
        with self.assertRaisesRegex(ValueError,'BODY_GEOMETRY_REQUIRED'): self.inspect()
        self.assertTrue((self.root/'source-contract.json').exists())

    def test_unspecified_shoe_articulation_is_rejected(self):
        self.config['shoes'].pop('articulation')
        with self.assertRaisesRegex(ValueError,'SHOE_POLICY_REQUIRED'): self.inspect()

if __name__=='__main__': unittest.main()
